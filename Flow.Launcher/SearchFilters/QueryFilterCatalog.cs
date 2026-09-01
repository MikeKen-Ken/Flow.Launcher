using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Flow.Launcher.SearchFilters;

internal static class QueryFilterCatalog
{
    internal static readonly (string Key, string[] Extensions)[] ExtensionGroups =
    [
        ("image", ["png", "jpg", "jpeg", "gif", "webp", "bmp", "svg", "ico", "heic"]),
        ("video", ["mp4", "mkv", "avi", "mov", "webm", "wmv"]),
        ("audio", ["mp3", "wav", "flac", "aac", "m4a", "ogg"]),
        ("document", ["pdf", "txt", "md", "docx", "xlsx", "pptx", "csv", "json", "xml", "html"]),
        ("archive", ["zip", "7z", "rar", "tar", "gz"]),
        ("exe", ["exe", "msi", "dll", "bat", "cmd", "ps1", "iso"])
    ];

    internal static readonly IReadOnlyList<string> ExtensionPresets =
        [.. ExtensionGroups.SelectMany(group => group.Extensions)];

    internal static readonly IReadOnlyList<string> DatePresets =
    [
        "today",
        "yesterday",
        "thisweek",
        "lastweek",
        "thismonth",
        "lastmonth",
        "thisyear",
        "lastyear"
    ];

    private static readonly Dictionary<string, QueryFilterId> PrefixMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["file"] = QueryFilterId.File,
        ["files"] = QueryFilterId.File,
        ["folder"] = QueryFilterId.Folder,
        ["folders"] = QueryFilterId.Folder,
        ["path"] = QueryFilterId.Path,
        ["size"] = QueryFilterId.Size,
        ["ext"] = QueryFilterId.Extension,
        ["da"] = QueryFilterId.DateAccessed,
        ["dateaccessed"] = QueryFilterId.DateAccessed,
        ["dm"] = QueryFilterId.DateModified,
        ["datemodified"] = QueryFilterId.DateModified,
        ["dc"] = QueryFilterId.DateCreated,
        ["datecreated"] = QueryFilterId.DateCreated
    };

    internal static QueryFilterGroup GroupOf(QueryFilterId id) => id switch
    {
        QueryFilterId.File or QueryFilterId.Folder => QueryFilterGroup.Kind,
        QueryFilterId.Path => QueryFilterGroup.Path,
        QueryFilterId.Size => QueryFilterGroup.Size,
        QueryFilterId.DateModified => QueryFilterGroup.DateModified,
        QueryFilterId.DateCreated => QueryFilterGroup.DateCreated,
        QueryFilterId.Extension => QueryFilterGroup.Extension,
        QueryFilterId.DateAccessed => QueryFilterGroup.DateAccessed,
        QueryFilterId.Hidden => QueryFilterGroup.Hidden,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
    };

    internal static bool RequiresValue(QueryFilterId id) =>
        id is QueryFilterId.Size or QueryFilterId.DateModified or QueryFilterId.DateCreated
            or QueryFilterId.Extension or QueryFilterId.DateAccessed or QueryFilterId.Path;

    internal static string Format(QueryFilterId id, string value) => id switch
    {
        QueryFilterId.File => "file:",
        QueryFilterId.Folder => "folder:",
        QueryFilterId.Path => QueryFilterPathValue.FormatCommand(value),
        QueryFilterId.Extension => $"ext:{QueryFilterExtensionValue.Join(QueryFilterExtensionValue.Parse(value))}",
        QueryFilterId.Size => $"size:{value}",
        QueryFilterId.DateModified => $"dm:{value}",
        QueryFilterId.DateCreated => $"dc:{value}",
        QueryFilterId.DateAccessed => $"da:{value}",
        QueryFilterId.Hidden => "attrib:H",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
    };

    internal static bool TryMatch(string token, [NotNullWhen(true)] out QueryFilterId? id, out string value)
    {
        id = null;
        value = string.Empty;

        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var colon = token.IndexOf(':');
        if (colon <= 0)
        {
            return false;
        }

        var prefix = token[..colon];
        value = token[(colon + 1)..];

        if (prefix.Equals("attrib", StringComparison.OrdinalIgnoreCase)
            || prefix.Equals("attributes", StringComparison.OrdinalIgnoreCase))
        {
            if (value.Contains('H', StringComparison.OrdinalIgnoreCase))
            {
                id = QueryFilterId.Hidden;
                value = "H";
                return true;
            }

            return false;
        }

        if (PrefixMap.TryGetValue(prefix, out var mapped))
        {
            if (mapped == QueryFilterId.Path)
            {
                if (!QueryFilterPathValue.TryNormalize(value, out var path))
                {
                    return false;
                }

                id = mapped;
                value = path;
                return true;
            }

            if (mapped == QueryFilterId.Extension)
            {
                var extensions = QueryFilterExtensionValue.Join(QueryFilterExtensionValue.Parse(value));
                if (string.IsNullOrEmpty(extensions))
                {
                    return false;
                }

                id = mapped;
                value = extensions;
                return true;
            }

            id = mapped;
            return true;
        }

        return false;
    }
}
