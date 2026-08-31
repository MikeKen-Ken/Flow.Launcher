using Flow.Launcher.Plugin;

namespace Flow.Launcher.History;

/// <summary>
/// An immutable-at-execution snapshot of the source and semantics of a history entry.
/// Stable plugin identity remains on <see cref="Result.PluginID"/>.
/// </summary>
public sealed class HistoryProvenance
{
    public string PluginName { get; set; } = string.Empty;

    public string PluginIconPath { get; set; } = string.Empty;

    public string ActionKeyword { get; set; } = string.Empty;

    public string SearchText { get; set; } = string.Empty;

    public string ActionId { get; set; } = string.Empty;

    public string ActionLabel { get; set; } = string.Empty;

    public HistoryActionKind ActionKind { get; set; } = HistoryActionKind.Unknown;

    public HistoryReplayMode ReplayMode { get; set; } = HistoryReplayMode.Execute;

    public static HistoryProvenance Capture(Result result, PluginMetadata pluginMetadata)
    {
        var action = result.HistoryAction;
        return new HistoryProvenance
        {
            PluginName = pluginMetadata?.Name ?? string.Empty,
            PluginIconPath = pluginMetadata?.IcoPath ?? string.Empty,
            ActionKeyword = result.OriginQuery?.ActionKeyword ?? string.Empty,
            SearchText = result.OriginQuery?.Search ?? string.Empty,
            ActionId = action?.Id ?? string.Empty,
            ActionLabel = action?.Label ?? string.Empty,
            ActionKind = action?.Kind ?? HistoryActionKind.Unknown,
            ReplayMode = action?.ReplayMode ?? HistoryReplayMode.Execute
        };
    }

    public static HistoryProvenance FromLegacy(string queryText, PluginMetadata pluginMetadata)
    {
        var actionKeyword = string.Empty;
        var searchText = queryText ?? string.Empty;

        if (pluginMetadata?.ActionKeywords != null)
        {
            foreach (var keyword in pluginMetadata.ActionKeywords)
            {
                if (string.IsNullOrEmpty(keyword) || keyword == Query.GlobalPluginWildcardSign)
                {
                    continue;
                }

                if (string.Equals(searchText, keyword, System.StringComparison.OrdinalIgnoreCase))
                {
                    actionKeyword = keyword;
                    searchText = string.Empty;
                    break;
                }

                var prefix = keyword + Query.TermSeparator;
                if (searchText.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    actionKeyword = keyword;
                    searchText = searchText[prefix.Length..];
                    break;
                }
            }
        }

        return new HistoryProvenance
        {
            PluginName = pluginMetadata?.Name ?? string.Empty,
            PluginIconPath = pluginMetadata?.IcoPath ?? string.Empty,
            ActionKeyword = actionKeyword,
            SearchText = searchText
        };
    }

    public HistoryProvenance DeepCopy()
    {
        return new HistoryProvenance
        {
            PluginName = PluginName,
            PluginIconPath = PluginIconPath,
            ActionKeyword = ActionKeyword,
            SearchText = SearchText,
            ActionId = ActionId,
            ActionLabel = ActionLabel,
            ActionKind = ActionKind,
            ReplayMode = ReplayMode
        };
    }
}
