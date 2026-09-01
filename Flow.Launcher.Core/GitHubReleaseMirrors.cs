using System;
using System.Collections.Generic;

namespace Flow.Launcher.Core
{
    /// <summary>
    /// GitHub release assets redirect to S3 / release-assets hosts that are often
    /// unreachable without a working proxy. Public prefix mirrors are tried only
    /// after the original GitHub URL fails.
    /// </summary>
    internal static class GitHubReleaseMirrors
    {
        internal static readonly string[] Prefixes =
        [
            "https://ghfast.top/",
            "https://ghproxy.net/"
        ];

        internal static IEnumerable<string> Candidates(string url)
        {
            if (CanMirror(url))
            {
                foreach (var prefix in Prefixes)
                    yield return prefix + url;
            }

            yield return url;
        }

        internal static bool CanMirror(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                return false;

            var path = uri.AbsolutePath;
            return path.Contains("/releases/download/", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/releases/latest/download/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
