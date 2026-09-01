using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Flow.Launcher.SearchFilters;

internal static class QueryFilterCatalog
{
    internal static readonly IReadOnlyList<string> SizePresets =
    [
        "empty",
        "tiny",
        "small",
        "medium",
        "large",
        "huge",
        "gigantic"
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
        ["pic"] = QueryFilterId.Image,
        ["picture"] = QueryFilterId.Image,
        ["video"] = QueryFilterId.Video,
        ["audio"] = QueryFilterId.Audio,
        ["size"] = QueryFilterId.Size,
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
        ["document"] = QueryFilterId.Document
    };

    internal static QueryFilterGroup GroupOf(QueryFilterId id) => id switch
    {
        QueryFilterId.File or QueryFilterId.Folder => QueryFilterGroup.Kind,
        QueryFilterId.Image or QueryFilterId.Video or QueryFilterId.Audio or QueryFilterId.Document => QueryFilterGroup.Type,
        QueryFilterId.Size => QueryFilterGroup.Size,
        QueryFilterId.DateModified => QueryFilterGroup.DateModified,
        QueryFilterId.DateCreated => QueryFilterGroup.DateCreated,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
    };

    internal static bool RequiresValue(QueryFilterId id) =>
        id is QueryFilterId.Size or QueryFilterId.DateModified or QueryFilterId.DateCreated;

    internal static string Format(QueryFilterId id, string value) => id switch
    {
        QueryFilterId.File => "file:",
        QueryFilterId.Folder => "folder:",
        QueryFilterId.Image => "type:image",
        QueryFilterId.Video => "type:video",
        QueryFilterId.Audio => "type:audio",
        QueryFilterId.Document => "type:document",
        QueryFilterId.Size => $"size:{value}",
        QueryFilterId.DateModified => $"dm:{value}",
        QueryFilterId.DateCreated => $"dc:{value}",
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

        if (PrefixMap.TryGetValue(prefix, out var mapped))
        {
            id = mapped;
            return true;
        }

        return false;
    }
}
