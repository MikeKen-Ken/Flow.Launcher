using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Flow.Launcher.Infrastructure.Http;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Core.WebDavSync;

public sealed class WebDavTransport : IWebDavTransport
{
    private static readonly string ClassName = nameof(WebDavTransport);
    private static readonly HttpMethod PropFindMethod = new("PROPFIND");
    private static readonly HttpMethod MkColMethod = new("MKCOL");
    private static readonly XNamespace DavNamespace = "DAV:";

    public async Task TestConnectionAsync(WebDavConnection connection, CancellationToken token = default)
    {
        using var client = CreateClient(connection);
        await EnsureDirectoryCoreAsync(client, NormalizeDirectoryUrl(connection.Url), token).ConfigureAwait(false);
    }

    public async Task EnsureDirectoryAsync(WebDavConnection connection, CancellationToken token = default)
    {
        using var client = CreateClient(connection);
        await EnsureDirectoryCoreAsync(client, NormalizeDirectoryUrl(connection.Url), token).ConfigureAwait(false);
    }

    public async Task<WebDavRemoteFileInfo> GetFileInfoAsync(
        WebDavConnection connection,
        string fileName,
        CancellationToken token = default)
    {
        using var client = CreateClient(connection);
        var url = CombineUrl(connection.Url, fileName);
        using var request = CreatePropFindRequest(url);
        using var response = await client.SendAsync(request, token).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return WebDavRemoteFileInfo.Missing;
        }

        if (!response.IsSuccessStatusCode && response.StatusCode != (HttpStatusCode)207)
        {
            throw new HttpRequestException(
                $"WebDAV PROPFIND failed with {(int)response.StatusCode} {response.ReasonPhrase} for {url}");
        }

        var xml = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        return ParseFileInfo(xml);
    }

    public async Task UploadFileAsync(
        WebDavConnection connection,
        string fileName,
        Stream content,
        CancellationToken token = default)
    {
        using var client = CreateClient(connection);
        await EnsureDirectoryCoreAsync(client, NormalizeDirectoryUrl(connection.Url), token).ConfigureAwait(false);

        var url = CombineUrl(connection.Url, fileName);
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await client.PutAsync(url, streamContent, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"WebDAV PUT failed with {(int)response.StatusCode} {response.ReasonPhrase} for {url}");
        }
    }

    public async Task DownloadFileAsync(
        WebDavConnection connection,
        string fileName,
        Stream destination,
        CancellationToken token = default)
    {
        using var client = CreateClient(connection);
        var url = CombineUrl(connection.Url, fileName);
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException($"Remote WebDAV file was not found: {fileName}", fileName);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"WebDAV GET failed with {(int)response.StatusCode} {response.ReasonPhrase} for {url}");
        }

        await using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await source.CopyToAsync(destination, token).ConfigureAwait(false);
    }

    internal static string CombineUrl(string baseUrl, string fileName)
    {
        var directory = NormalizeDirectoryUrl(baseUrl);
        return directory + Uri.EscapeDataString(fileName);
    }

    internal static string NormalizeDirectoryUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("WebDAV URL is required.", nameof(url));
        }

        return url.Trim().TrimEnd('/') + "/";
    }

    private static HttpClient CreateClient(WebDavConnection connection)
    {
        var handler = new HttpClientHandler
        {
            PreAuthenticate = true,
            AllowAutoRedirect = true
        };

        if (Http.WebProxy.Address != null)
        {
            handler.Proxy = Http.WebProxy;
            handler.UseProxy = true;
        }

        if (!string.IsNullOrEmpty(connection.UserName))
        {
            handler.Credentials = new NetworkCredential(connection.UserName, connection.Password ?? string.Empty);
        }

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        client.DefaultRequestHeaders.ExpectContinue = false;
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Flow.Launcher-WebDAV-Sync");

        if (!string.IsNullOrEmpty(connection.UserName))
        {
            var raw = $"{connection.UserName}:{connection.Password ?? string.Empty}";
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        }

        return client;
    }

    private static async Task EnsureDirectoryCoreAsync(HttpClient client, string directoryUrl, CancellationToken token)
    {
        using (var probe = CreatePropFindRequest(directoryUrl))
        using (var probeResponse = await client.SendAsync(probe, token).ConfigureAwait(false))
        {
            if (probeResponse.IsSuccessStatusCode || probeResponse.StatusCode == (HttpStatusCode)207)
            {
                return;
            }

            if (probeResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new HttpRequestException(
                    $"WebDAV authentication failed with {(int)probeResponse.StatusCode} {probeResponse.ReasonPhrase}");
            }

            if (probeResponse.StatusCode != HttpStatusCode.NotFound &&
                probeResponse.StatusCode != HttpStatusCode.Conflict)
            {
                throw new HttpRequestException(
                    $"WebDAV folder check failed with {(int)probeResponse.StatusCode} {probeResponse.ReasonPhrase}");
            }
        }

        await CreateCollectionRecursiveAsync(client, new Uri(directoryUrl), token).ConfigureAwait(false);
    }

    private static async Task CreateCollectionRecursiveAsync(HttpClient client, Uri uri, CancellationToken token)
    {
        if (uri.AbsolutePath is "/" or "")
        {
            return;
        }

        var parentPath = string.Concat(uri.Segments.Take(uri.Segments.Length - 1));
        if (!string.IsNullOrEmpty(parentPath) && parentPath != "/")
        {
            var parentUri = new Uri(uri, parentPath);
            using var parentProbe = CreatePropFindRequest(parentUri.AbsoluteUri);
            using var parentResponse = await client.SendAsync(parentProbe, token).ConfigureAwait(false);
            if (parentResponse.StatusCode == HttpStatusCode.NotFound)
            {
                await CreateCollectionRecursiveAsync(client, parentUri, token).ConfigureAwait(false);
            }
            else if (!parentResponse.IsSuccessStatusCode &&
                     parentResponse.StatusCode != (HttpStatusCode)207 &&
                     parentResponse.StatusCode != HttpStatusCode.MethodNotAllowed)
            {
                Log.Info(ClassName, $"Parent PROPFIND returned {(int)parentResponse.StatusCode} for {parentUri}");
            }
        }

        using var request = new HttpRequestMessage(MkColMethod, uri);
        using var response = await client.SendAsync(request, token).ConfigureAwait(false);
        if (response.IsSuccessStatusCode ||
            response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.Conflict)
        {
            return;
        }

        throw new HttpRequestException(
            $"WebDAV MKCOL failed with {(int)response.StatusCode} {response.ReasonPhrase} for {uri}");
    }

    private static HttpRequestMessage CreatePropFindRequest(string url)
    {
        var request = new HttpRequestMessage(PropFindMethod, url);
        request.Headers.Add("Depth", "0");
        request.Content = new StringContent(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <d:propfind xmlns:d="DAV:">
              <d:prop>
                <d:getlastmodified/>
                <d:getcontentlength/>
                <d:resourcetype/>
              </d:prop>
            </d:propfind>
            """,
            Encoding.UTF8,
            "application/xml");
        return request;
    }

    private static WebDavRemoteFileInfo ParseFileInfo(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return new WebDavRemoteFileInfo { Exists = true };
        }

        try
        {
            var document = XDocument.Parse(xml);
            var prop = document.Descendants(DavNamespace + "prop").FirstOrDefault();
            if (prop == null)
            {
                return new WebDavRemoteFileInfo { Exists = true };
            }

            DateTime? lastModified = null;
            var modifiedText = prop.Element(DavNamespace + "getlastmodified")?.Value;
            if (!string.IsNullOrWhiteSpace(modifiedText) &&
                DateTime.TryParse(modifiedText, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
            {
                lastModified = parsed.ToUniversalTime();
            }

            long? length = null;
            var lengthText = prop.Element(DavNamespace + "getcontentlength")?.Value;
            if (long.TryParse(lengthText, out var parsedLength))
            {
                length = parsedLength;
            }

            return new WebDavRemoteFileInfo
            {
                Exists = true,
                LastModifiedUtc = lastModified,
                Length = length
            };
        }
        catch (Exception e)
        {
            Log.Info(ClassName, $"Unable to parse WebDAV PROPFIND XML: {e.Message}");
            return new WebDavRemoteFileInfo { Exists = true };
        }
    }
}
