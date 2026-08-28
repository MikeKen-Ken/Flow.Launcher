using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Core.WebDavSync;

public sealed class WebDavSyncService
{
    private static readonly string ClassName = nameof(WebDavSyncService);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly IWebDavTransport _transport;
    private readonly WebDavSyncPaths _paths;

    public WebDavSyncService() : this(new WebDavTransport(), WebDavSyncPaths.FromDataLocation())
    {
    }

    public WebDavSyncService(IWebDavTransport transport, WebDavSyncPaths paths)
    {
        _transport = transport;
        _paths = paths;
    }

    public async Task TestConnectionAsync(WebDavSyncSettings settings, CancellationToken token = default)
    {
        ValidateSettings(settings);
        await _transport.TestConnectionAsync(ToConnection(settings), token).ConfigureAwait(false);
    }

    public async Task<WebDavSyncResult> ExecuteAsync(
        WebDavSyncOperation operation,
        WebDavSyncSettings settings,
        Action<double> reportProgress = null,
        CancellationToken token = default)
    {
        try
        {
            ValidateSettings(settings);
            if (!settings.SyncSettings && !settings.SyncPlugins)
            {
                return WebDavSyncResult.Failed(Localize.webDavSyncNothingSelected());
            }

            reportProgress?.Invoke(5);
            var connection = ToConnection(settings);

            return operation switch
            {
                WebDavSyncOperation.Upload => await UploadAsync(connection, settings, reportProgress, token)
                    .ConfigureAwait(false),
                WebDavSyncOperation.Download => await DownloadAsync(connection, settings, reportProgress, token)
                    .ConfigureAwait(false),
                WebDavSyncOperation.Sync => await SyncAsync(connection, settings, reportProgress, token)
                    .ConfigureAwait(false),
                _ => WebDavSyncResult.Failed($"Unknown WebDAV sync operation: {operation}")
            };
        }
        catch (OperationCanceledException)
        {
            return WebDavSyncResult.Failed(Localize.webDavSyncCancelled());
        }
        catch (Exception e)
        {
            Log.Error(ClassName, $"WebDAV sync failed: {e.Message}");
            return WebDavSyncResult.Failed(e.Message);
        }
    }

    private async Task<WebDavSyncResult> SyncAsync(
        WebDavConnection connection,
        WebDavSyncSettings settings,
        Action<double> reportProgress,
        CancellationToken token)
    {
        reportProgress?.Invoke(10);
        var remoteInfo = await _transport.GetFileInfoAsync(
            connection, WebDavSyncConstants.RemoteZipFileName, token).ConfigureAwait(false);
        DateTime? remoteTime = remoteInfo.Exists ? remoteInfo.LastModifiedUtc : null;

        if (remoteInfo.Exists)
        {
            remoteTime = await TryReadRemoteManifestTimeAsync(connection, remoteTime, token).ConfigureAwait(false);
        }

        var localTime = WebDavSyncArchive.GetLocalMaxWriteUtc(_paths, settings.SyncSettings, settings.SyncPlugins);
        var action = WebDavSyncPlanner.Decide(localTime, settings.LastSuccessfulSyncUtc, remoteTime);

        return action switch
        {
            WebDavSyncActionTaken.Uploaded => await UploadAsync(connection, settings, reportProgress, token)
                .ConfigureAwait(false),
            WebDavSyncActionTaken.Downloaded => await DownloadAsync(connection, settings, reportProgress, token)
                .ConfigureAwait(false),
            _ => WebDavSyncResult.Ok(WebDavSyncActionTaken.AlreadyInSync)
        };
    }

    private async Task<WebDavSyncResult> UploadAsync(
        WebDavConnection connection,
        WebDavSyncSettings settings,
        Action<double> reportProgress,
        CancellationToken token)
    {
        var workDirectory = Path.Combine(Path.GetTempPath(), "FlowLauncherWebDavUpload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);
        var zipPath = Path.Combine(workDirectory, WebDavSyncConstants.RemoteZipFileName);
        var manifestPath = Path.Combine(workDirectory, WebDavSyncConstants.RemoteManifestFileName);

        try
        {
            reportProgress?.Invoke(20);
            var manifest = WebDavSyncArchive.CreateZip(
                zipPath, _paths, settings.SyncSettings, settings.SyncPlugins);
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));

            reportProgress?.Invoke(45);
            await using (var zipStream = File.OpenRead(zipPath))
            {
                await _transport.UploadFileAsync(
                    connection, WebDavSyncConstants.RemoteZipFileName, zipStream, token).ConfigureAwait(false);
            }

            reportProgress?.Invoke(80);
            await using (var manifestStream = File.OpenRead(manifestPath))
            {
                await _transport.UploadFileAsync(
                    connection, WebDavSyncConstants.RemoteManifestFileName, manifestStream, token)
                    .ConfigureAwait(false);
            }

            settings.LastSuccessfulSyncUtc = manifest.ExportedAtUtc;
            settings.LastResult = nameof(WebDavSyncActionTaken.Uploaded);
            reportProgress?.Invoke(100);
            return WebDavSyncResult.Ok(WebDavSyncActionTaken.Uploaded);
        }
        finally
        {
            WebDavSyncArchive.TryDeleteDirectory(workDirectory);
        }
    }

    private async Task<WebDavSyncResult> DownloadAsync(
        WebDavConnection connection,
        WebDavSyncSettings settings,
        Action<double> reportProgress,
        CancellationToken token)
    {
        var workDirectory = Path.Combine(Path.GetTempPath(), "FlowLauncherWebDavDownload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);
        var zipPath = Path.Combine(workDirectory, WebDavSyncConstants.RemoteZipFileName);

        try
        {
            reportProgress?.Invoke(20);
            var remoteInfo = await _transport.GetFileInfoAsync(
                connection, WebDavSyncConstants.RemoteZipFileName, token).ConfigureAwait(false);
            if (!remoteInfo.Exists)
            {
                return WebDavSyncResult.Failed(Localize.webDavSyncRemoteMissing());
            }

            reportProgress?.Invoke(40);
            await using (var zipStream = File.Create(zipPath))
            {
                await _transport.DownloadFileAsync(
                    connection, WebDavSyncConstants.RemoteZipFileName, zipStream, token).ConfigureAwait(false);
            }

            reportProgress?.Invoke(75);
            var manifest = WebDavSyncArchive.ReadManifest(zipPath);
            WebDavPendingApply.StageDownloadedZip(zipPath, _paths.DataDirectory);

            settings.LastSuccessfulSyncUtc = manifest.ExportedAtUtc == default
                ? DateTime.UtcNow
                : manifest.ExportedAtUtc;
            settings.LastResult = nameof(WebDavSyncActionTaken.Downloaded);
            reportProgress?.Invoke(100);
            return WebDavSyncResult.Ok(WebDavSyncActionTaken.Downloaded, requiresRestart: true);
        }
        finally
        {
            WebDavSyncArchive.TryDeleteDirectory(workDirectory);
        }
    }

    private async Task<DateTime?> TryReadRemoteManifestTimeAsync(
        WebDavConnection connection,
        DateTime? fallback,
        CancellationToken token)
    {
        try
        {
            var info = await _transport.GetFileInfoAsync(
                connection, WebDavSyncConstants.RemoteManifestFileName, token).ConfigureAwait(false);
            if (!info.Exists)
            {
                return fallback;
            }

            using var buffer = new MemoryStream();
            await _transport.DownloadFileAsync(
                connection, WebDavSyncConstants.RemoteManifestFileName, buffer, token).ConfigureAwait(false);
            buffer.Position = 0;
            var manifest = await JsonSerializer.DeserializeAsync<WebDavSyncManifest>(buffer, JsonOptions, token)
                .ConfigureAwait(false);
            if (manifest?.ExportedAtUtc != default)
            {
                return manifest.ExportedAtUtc;
            }
        }
        catch (Exception e)
        {
            Log.Info(ClassName, $"Unable to read remote WebDAV manifest: {e.Message}");
        }

        return fallback;
    }

    private static WebDavConnection ToConnection(WebDavSyncSettings settings) => new()
    {
        Url = settings.Url,
        UserName = settings.UserName,
        Password = settings.Password
    };

    private static void ValidateSettings(WebDavSyncSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (string.IsNullOrWhiteSpace(settings.Url))
        {
            throw new InvalidOperationException(Localize.webDavSyncUrlRequired());
        }

        if (!Uri.TryCreate(settings.Url.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(Localize.webDavSyncUrlInvalid());
        }
    }
}
