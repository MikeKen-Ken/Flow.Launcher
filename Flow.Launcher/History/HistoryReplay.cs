using System;
using System.Linq;
using Flow.Launcher.Plugin;
using Flow.Launcher.Storage;

namespace Flow.Launcher.History;

/// <summary>
/// Reconstructs plugin queries and validates semantic matches for history replay.
/// </summary>
public static class HistoryReplay
{
    public static Query BuildQuery(LastOpenedHistoryResult item, PluginMetadata metadata)
    {
        var provenance = item.Provenance;
        if (provenance == null)
        {
            return null;
        }

        var searchText = provenance.SearchText ?? string.Empty;
        var actionKeyword = ResolveCurrentActionKeyword(provenance, metadata);
        var queryText = BuildQueryText(actionKeyword, searchText);
        var searchTerms = searchText
            .Split(Query.TermSeparator, StringSplitOptions.RemoveEmptyEntries);

        return new Query
        {
            OriginalQuery = queryText,
            TrimmedQuery = queryText,
            ActionKeyword = actionKeyword,
            Search = searchText,
            SearchTerms = searchTerms,
            IsHomeQuery = string.IsNullOrEmpty(queryText)
        };
    }

    public static string BuildQueryText(LastOpenedHistoryResult item, PluginMetadata metadata)
    {
        if (item.Provenance == null)
        {
            return item.Query;
        }

        return BuildQueryText(
            ResolveCurrentActionKeyword(item.Provenance, metadata),
            item.Provenance.SearchText);
    }

    public static bool IsSemanticMatch(LastOpenedHistoryResult item, Result result)
    {
        var actionId = item.Provenance?.ActionId;
        return string.IsNullOrEmpty(actionId)
            || string.Equals(actionId, result.HistoryAction?.Id, StringComparison.Ordinal);
    }

    private static string ResolveCurrentActionKeyword(HistoryProvenance provenance, PluginMetadata metadata)
    {
        if (string.IsNullOrEmpty(provenance.ActionKeyword))
        {
            return string.Empty;
        }

        var savedKeyword = metadata?.ActionKeywords?
            .FirstOrDefault(keyword => string.Equals(
                keyword,
                provenance.ActionKeyword,
                StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(savedKeyword)
            && savedKeyword != Query.GlobalPluginWildcardSign)
        {
            return savedKeyword;
        }

        if (!string.IsNullOrEmpty(metadata?.ActionKeyword)
            && metadata.ActionKeyword != Query.GlobalPluginWildcardSign)
        {
            return metadata.ActionKeyword;
        }

        return metadata?.ActionKeywords?
            .FirstOrDefault(keyword => !string.IsNullOrEmpty(keyword)
                && keyword != Query.GlobalPluginWildcardSign)
            ?? string.Empty;
    }

    private static string BuildQueryText(string actionKeyword, string searchText)
    {
        if (string.IsNullOrEmpty(actionKeyword))
        {
            return searchText ?? string.Empty;
        }

        if (string.IsNullOrEmpty(searchText))
        {
            return actionKeyword;
        }

        return $"{actionKeyword}{Query.TermSeparator}{searchText}";
    }
}
