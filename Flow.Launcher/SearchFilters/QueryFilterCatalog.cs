using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Flow.Launcher.SearchFilters;

internal static class QueryFilterCatalog
{
    internal static readonly IReadOnlyList<string> ExtensionPresets =
    [
        "pdf",
        "zip",
        "7z",
        "txt",
        "md",
        "csv",
        "json",
        "xml",
        "html",
        "xlsx",
        "docx",
        "pptx"
    ];

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
        ["pic"] = QueryFilterId.Image,
        ["picture"] = QueryFilterId.Image,
        ["video"] = QueryFilterId.Video,
        ["audio"] = QueryFilterId.Audio,
        ["size"] = QueryFilterId.Size,
        ["zip"] = QueryFilterId.Archive,
        ["archive"] = QueryFilterId.Archive,
        ["exe"] = QueryFilterId.Executable,
        ["ext"] = QueryFilterId.Extension,
        ["da"] = QueryFilterId.DateAccessed,
        ["dateaccessed"] = QueryFilterId.DateAccessed,
        ["dm"] = QueryFilterId.DateModified,
        ["datemodified"] = QueryFilterId.DateModified,
        ["dc"] = QueryFilterId.DateCreated,
        ["datecreated"] = QueryFilterId.DateCreated
    };

    private static readonly Dictionary<string, QueryFilterId> TypeValueMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image"] = QueryFilterId.Image,
        ["pic"] = QueryFilterId.Image,
        ["picture"] = QueryFilterId.Image,
        ["video"] = QueryFilterId.Video,
        ["audio"] = QueryFilterId.Audio,
        ["doc"] = QueryFilterId.Document,
        ["document"] = QueryFilterId.Document,
        ["archive"] = QueryFilterId.Archive,
        ["zip"] = QueryFilterId.Archive,
        ["exe"] = QueryFilterId.Executable
    };

    internal static QueryFilterGroup GroupOf(QueryFilterId id) => id switch
    {
        QueryFilterId.File or QueryFilterId.Folder => QueryFilterGroup.Kind,
        QueryFilterId.Path => QueryFilterGroup.Path,
        QueryFilterId.Image or QueryFilterId.Video or QueryFilterId.Audio or QueryFilterId.Document
            or QueryFilterId.Archive or QueryFilterId.Executable => QueryFilterGroup.Type,
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
        QueryFilterId.Path => QueryFilterPathValue.FormatToken(value),
        QueryFilterId.Image => "type:image",
        QueryFilterId.Video => "type:video",
        QueryFilterId.Audio => "type:audio",
        QueryFilterId.Document => "type:document",
        QueryFilterId.Archive => "type:archive",
        QueryFilterId.Executable => "type:exe",
        QueryFilterId.Extension => $"ext:{value}",
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

        if (prefix.Equals("type", StringComparison.OrdinalIgnoreCase))
        {
            if (TypeValueMap.TryGetValue(value, out var typeId))
            {
                id = typeId;
                return true;
            }

            return false;
        }

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

            id = mapped;
            return true;
        }

        return false;
    }
}
