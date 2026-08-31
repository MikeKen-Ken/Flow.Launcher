using System;
using System.IO;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Http;
using Squirrel;

namespace Flow.Launcher.Core
{
    /// <summary>
    /// Squirrel's <see cref="FileDownloader"/> disposes a shared <see cref="System.Net.WebClient"/>,
    /// retries by lowercasing the URL (breaking GitHub/S3 signed paths), and does not send a User-Agent.
    /// </summary>
    internal sealed class HttpFileDownloader : IFileDownloader
    {
        public async Task DownloadFile(string url, string targetFile, Action<int> progress)
        {
            if (File.Exists(targetFile))
                File.Delete(targetFile);

            Action<double> report = progress == null
                ? null
                : p => progress((int)Math.Clamp(Math.Round(p), 0, 100));

            await Http.DownloadAsync(url, targetFile, report).ConfigureAwait(false);
            progress?.Invoke(100);
        }

        public async Task<byte[]> DownloadUrl(string url)
        {
            await using var stream = await Http.GetStreamAsync(url).ConfigureAwait(false);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory).ConfigureAwait(false);
            return memory.ToArray();
        }
    }
}
