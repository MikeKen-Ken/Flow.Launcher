using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Core.WebDavSync;

public interface IWebDavTransport
{
    Task TestConnectionAsync(WebDavConnection connection, CancellationToken token = default);

    Task EnsureDirectoryAsync(WebDavConnection connection, CancellationToken token = default);

    Task<WebDavRemoteFileInfo> GetFileInfoAsync(
        WebDavConnection connection,
        string fileName,
        CancellationToken token = default);

    Task UploadFileAsync(
        WebDavConnection connection,
        string fileName,
        Stream content,
        CancellationToken token = default);

    Task DownloadFileAsync(
        WebDavConnection connection,
        string fileName,
        Stream destination,
        CancellationToken token = default);
}
