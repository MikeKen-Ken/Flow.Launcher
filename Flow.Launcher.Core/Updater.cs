using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using JetBrains.Annotations;
using Squirrel;

namespace Flow.Launcher.Core
{
    public class Updater
    {
        public string GitHubRepository { get; init; }

        private static readonly string ClassName = nameof(Updater);

        private readonly IPublicAPI _api;

        public Updater(IPublicAPI publicAPI, string gitHubRepository)
        {
            _api = publicAPI;
            GitHubRepository = gitHubRepository;
        }

        private SemaphoreSlim UpdateLock { get; } = new SemaphoreSlim(1);

        public async Task UpdateAppAsync(bool silentUpdate = true)
        {
            if (!silentUpdate)
            {
                if (!await UpdateLock.WaitAsync(TimeSpan.Zero).ConfigureAwait(false))
                {
                    _api.ShowMsgBox(Localize.update_flowlauncher_already_checking(),
                        Localize.update_flowlauncher_update_check());
                    return;
                }
            }
            else
            {
                await UpdateLock.WaitAsync().ConfigureAwait(false);
            }

            _api.LogInfo(ClassName, silentUpdate ? "Starting silent update check" : "Starting manual update check");

            try
            {
                if (silentUpdate)
                {
                    var silentResult = await ExecuteUpdateAsync(null, CancellationToken.None).ConfigureAwait(false);
                    if (silentResult.Status == UpdateStatus.Applied)
                        PresentUpdateResult(silentResult);
                    return;
                }

                using var cts = new CancellationTokenSource();
                UpdateAttemptResult result = null;

                await _api.ShowProgressBoxAsync(
                    Localize.update_flowlauncher_update_check(),
                    async reportProgress =>
                    {
                        // ProgressBoxEx re-invokes this callback with null after an exception.
                        if (reportProgress == null)
                            return;

                        try
                        {
                            result = await ExecuteUpdateAsync(reportProgress, cts.Token).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            result = UpdateAttemptResult.Failed(e);
                        }
                    },
                    cts.Cancel).ConfigureAwait(false);

                if (cts.IsCancellationRequested || result?.Status == UpdateStatus.Cancelled)
                    return;

                PresentUpdateResult(result);
            }
            catch (Exception e)
            {
                PresentUpdateFailure(e, silentUpdate);
            }
            finally
            {
                UpdateLock.Release();
            }
        }

        private async Task<UpdateAttemptResult> ExecuteUpdateAsync(
            Action<double> reportProgress,
            CancellationToken token)
        {
            ReportProgress(reportProgress, 5);

            using var updateManager = await GitHubUpdateManagerAsync(GitHubRepository, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();

            // CheckForUpdate returns a useful result only when the app is Squirrel-installed.
            var newUpdateInfo = await updateManager
                .CheckForUpdate(progress: p => ReportProgress(reportProgress, 5 + p * 0.25))
                .ConfigureAwait(false);

            if (newUpdateInfo?.FutureReleaseEntry == null)
            {
                _api.LogInfo(ClassName, "CheckForUpdate returned no future release. This install may not support in-app updates.");
                return UpdateAttemptResult.NotAvailable();
            }

            var newReleaseVersion =
                SemanticVersioning.Version.Parse(newUpdateInfo.FutureReleaseEntry.Version.ToString());
            var currentVersion = SemanticVersioning.Version.Parse(Constant.Version);

            _api.LogInfo(ClassName, $"Future Release <{Formatted(newUpdateInfo.FutureReleaseEntry)}>");

            if (newReleaseVersion <= currentVersion)
                return UpdateAttemptResult.AlreadyLatest();

            token.ThrowIfCancellationRequested();
            ReportProgress(reportProgress, 30);

            await updateManager
                .DownloadReleases(newUpdateInfo.ReleasesToApply, p => ReportProgress(reportProgress, 30 + p * 0.55))
                .ConfigureAwait(false);

            token.ThrowIfCancellationRequested();
            ReportProgress(reportProgress, 85);

            await updateManager.ApplyReleases(newUpdateInfo).ConfigureAwait(false);

            if (DataLocation.PortableDataLocationInUse())
            {
                var targetDestination = updateManager.RootAppDirectory +
                                        $"\\app-{newReleaseVersion}\\{DataLocation.PortableFolderName}";
                FilesFolders.CopyAll(DataLocation.PortableDataPath, targetDestination, (s) => _api.ShowMsgBox(s));
                if (!FilesFolders.VerifyBothFolderFilesEqual(DataLocation.PortableDataPath, targetDestination,
                        (s) => _api.ShowMsgBox(s)))
                    _api.ShowMsgBox(Localize.update_flowlauncher_fail_moving_portable_user_profile_data(
                        DataLocation.PortableDataPath, targetDestination));
            }
            else
            {
                await updateManager.CreateUninstallerRegistryEntry().ConfigureAwait(false);
            }

            var newVersionTips = NewVersionTips(newReleaseVersion.ToString());
            _api.LogInfo(ClassName, $"Update success:{newVersionTips}");
            ReportProgress(reportProgress, 99);

            return UpdateAttemptResult.Applied(newVersionTips);
        }

        private void PresentUpdateResult(UpdateAttemptResult result)
        {
            if (result == null)
                return;

            switch (result.Status)
            {
                case UpdateStatus.AlreadyLatest:
                    _api.ShowMsgBox(Localize.update_flowlauncher_already_on_latest());
                    return;
                case UpdateStatus.NotAvailable:
                    _api.ShowMsgBox(Localize.update_flowlauncher_not_squirrel(),
                        Localize.update_flowlauncher_fail());
                    return;
                case UpdateStatus.Applied:
                    if (_api.ShowMsgBox(result.NewVersionTips, Localize.update_flowlauncher_new_update(),
                            MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        UpdateManager.RestartApp(Constant.ApplicationFileName);
                    }
                    return;
                case UpdateStatus.Failed:
                    PresentUpdateFailure(result.Error, silentUpdate: false);
                    return;
            }
        }

        private void PresentUpdateFailure(Exception e, bool silentUpdate)
        {
            if (e is OperationCanceledException)
                return;

            if (e is HttpRequestException or WebException or SocketException ||
                e.InnerException is TimeoutException)
            {
                _api.LogException(ClassName,
                    "Check your connection and proxy settings for GitHub releases (api.github.com, github.com, release-assets.githubusercontent.com).", e);
            }
            else
            {
                _api.LogException(ClassName, $"Error Occurred", e);
            }

            if (silentUpdate)
                return;

            var detail = e is HttpRequestException or WebException or SocketException ||
                         e.InnerException is TimeoutException
                ? Localize.update_flowlauncher_check_connection()
                : string.IsNullOrWhiteSpace(e.Message)
                    ? Localize.update_flowlauncher_update_error()
                    : e.Message;

            _api.ShowMsgBox(detail, Localize.update_flowlauncher_fail());
        }

        [UsedImplicitly]
        private class GithubRelease
        {
            [JsonPropertyName("prerelease")] public bool Prerelease { get; [UsedImplicitly] set; }

            [JsonPropertyName("published_at")] public DateTime PublishedAt { get; [UsedImplicitly] set; }

            [JsonPropertyName("html_url")] public string HtmlUrl { get; [UsedImplicitly] set; }
        }

        // https://github.com/Squirrel/Squirrel.Windows/blob/master/src/Squirrel/UpdateManager.Factory.cs
        private static async Task<UpdateManager> GitHubUpdateManagerAsync(
            string repository,
            CancellationToken token = default)
        {
            var uri = new Uri(repository);
            var api = $"https://api.github.com/repos{uri.AbsolutePath}/releases";

            await using var jsonStream = await Http.GetStreamAsync(api, token).ConfigureAwait(false);

            var releases = await JsonSerializer
                .DeserializeAsync<List<GithubRelease>>(jsonStream, cancellationToken: token)
                .ConfigureAwait(false);
            var latest = releases?
                .Where(r => !r.Prerelease)
                .OrderByDescending(r => r.PublishedAt)
                .FirstOrDefault();

            if (latest == null || string.IsNullOrEmpty(latest.HtmlUrl))
                throw new InvalidOperationException("No stable GitHub releases were found.");

            var latestUrl = latest.HtmlUrl.Replace("/tag/", "/download/");

            var manager = new UpdateManager(latestUrl, urlDownloader: new HttpFileDownloader());

            return manager;
        }

        private static string NewVersionTips(string version)
        {
            var tips = Localize.newVersionTips(version);

            return tips;
        }

        private static string Formatted<T>(T t)
        {
            var formatted = JsonSerializer.Serialize(t, new JsonSerializerOptions { WriteIndented = true });

            return formatted;
        }

        private static void ReportProgress(Action<double> reportProgress, double value)
        {
            if (reportProgress == null)
                return;

            reportProgress(Math.Clamp(value, 0, 99));
        }

        private enum UpdateStatus
        {
            AlreadyLatest,
            Applied,
            NotAvailable,
            Cancelled,
            Failed
        }

        private sealed class UpdateAttemptResult
        {
            public UpdateStatus Status { get; private init; }
            public string NewVersionTips { get; private init; }
            public Exception Error { get; private init; }

            public static UpdateAttemptResult AlreadyLatest() => new() { Status = UpdateStatus.AlreadyLatest };

            public static UpdateAttemptResult Applied(string newVersionTips) => new()
            {
                Status = UpdateStatus.Applied,
                NewVersionTips = newVersionTips
            };

            public static UpdateAttemptResult NotAvailable() => new() { Status = UpdateStatus.NotAvailable };

            public static UpdateAttemptResult Failed(Exception error) =>
                error is OperationCanceledException
                    ? new UpdateAttemptResult { Status = UpdateStatus.Cancelled }
                    : new UpdateAttemptResult { Status = UpdateStatus.Failed, Error = error };
        }
    }
}
