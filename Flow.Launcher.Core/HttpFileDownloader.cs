using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;
using Squirrel;

namespace Flow.Launcher.Core
{
    /// <summary>
    /// Squirrel's <see cref="FileDownloader"/> disposes a shared <see cref="System.Net.WebClient"/>,
    /// retries by lowercasing the URL (breaking GitHub/S3 signed paths), and does not send a User-Agent.
    /// Direct github.com asset URLs often hang until timeout in some networks, so those attempts
    /// are cancelled quickly and retried through public prefix mirrors.
    /// </summary>
    internal sealed class HttpFileDownloader : IFileDownloader
    {
        private static readonly string ClassName = nameof(HttpFileDownloader);
        private static readonly TimeSpan DirectGitHubAttemptTimeout = TimeSpan.FromSeconds(10);

        public async Task DownloadFile(string url, string targetFile, Action<int> progress)
        {
            Action<double> report = progress == null
                ? null
                : p => progress((int)Math.Clamp(Math.Round(p), 0, 100));

            await DownloadWithFallbackAsync(url, async (downloadUrl, token) =>
            {
                if (File.Exists(targetFile))
                    File.Delete(targetFile);

                await Http.DownloadAsync(downloadUrl, targetFile, report, token).ConfigureAwait(false);
            }, failFastOnDirectGitHub: false).ConfigureAwait(false);

            progress?.Invoke(100);
        }

        public async Task<byte[]> DownloadUrl(string url)
        {
            byte[] bytes = null;

            await DownloadWithFallbackAsync(url, async (downloadUrl, token) =>
            {
                await using var stream = await Http.GetStreamAsync(downloadUrl, token).ConfigureAwait(false);
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, token).ConfigureAwait(false);
                bytes = memory.ToArray();
            }, failFastOnDirectGitHub: true).ConfigureAwait(false);

            return bytes;
        }

        private static async Task DownloadWithFallbackAsync(
            string url,
            Func<string, CancellationToken, Task> download,
            bool failFastOnDirectGitHub)
        {
            Exception lastError = null;

            foreach (var candidate in GitHubReleaseMirrors.Candidates(url))
            {
                using var cts = new CancellationTokenSource();
                if (failFastOnDirectGitHub && GitHubReleaseMirrors.CanMirror(candidate))
                    cts.CancelAfter(DirectGitHubAttemptTimeout);

                try
                {
                    await download(candidate, cts.Token).ConfigureAwait(false);
                    if (!string.Equals(candidate, url, StringComparison.Ordinal))
                        Log.Info(ClassName, $"Downloaded GitHub asset via mirror: {candidate}");
                    return;
                }
                catch (Exception e) when (IsRetryableDownloadError(e))
                {
                    lastError = e;
                    Log.Warn(ClassName, $"Download failed from {candidate}: {e.Message}");
                }
            }

            throw lastError ?? new HttpRequestException($"Failed to download {url}");
        }

        private static bool IsRetryableDownloadError(Exception e) =>
            e is HttpRequestException or TaskCanceledException or OperationCanceledException;
    }
}
