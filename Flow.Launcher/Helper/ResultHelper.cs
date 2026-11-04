using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.History;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;
using Flow.Launcher.Storage;

namespace Flow.Launcher.Helper;

#nullable enable

public static class ResultHelper
{
    public static Task<Result?> PopulateResultsAsync(LastOpenedHistoryResult item)
    {
        return PopulateResultsAsync(item, matchAction: true);
    }

    /// <summary>
    /// Re-runs a context-menu command saved in history.
    /// Returns null when the source result or the menu command can no longer be found.
    /// </summary>
    public static async Task<bool?> TryExecuteContextMenuAsync(LastOpenedHistoryResult item, ActionContext context)
    {
        var source = await PopulateResultsAsync(item, matchAction: false).ConfigureAwait(false);
        if (source == null)
            return null;

        List<Result> menus;
        try
        {
            menus = await Win32Helper.StartSTATaskAsync(() => PluginManager.GetContextMenusForPlugin(source))
                .ConfigureAwait(false);
        }
        catch (System.Exception e)
        {
            App.API.LogException(nameof(ResultHelper), "Failed to reload a context menu from history", e);
            return null;
        }

        var menu = ContextMenuHistory.FindMenuItem(menus, item.Provenance);
        if (menu == null)
            return null;

        return await menu.ExecuteAsync(context).ConfigureAwait(false);
    }

    private static async Task<Result?> PopulateResultsAsync(LastOpenedHistoryResult item, bool matchAction)
    {
        var plugin = PluginManager.GetPluginForId(item.PluginID);
        if (plugin == null) return null;

        var query = HistoryReplay.BuildQuery(item, plugin.Metadata)
            ?? QueryBuilder.Build(item.Query, item.Query, PluginManager.GetNonGlobalPlugins());
        if (query == null) return null;

        try
        {
            var freshResults = await PluginManager.QueryForPluginAsync(plugin, query, CancellationToken.None);
            var candidates = freshResults?.AsEnumerable() ?? Enumerable.Empty<Result>();
            if (matchAction)
                candidates = candidates.Where(r => HistoryReplay.IsSemanticMatch(item, r));

            if (string.IsNullOrEmpty(item.RecordKey))
            {
                return candidates.FirstOrDefault(r => r.Title == item.Title && r.SubTitle == item.SubTitle);
            }

            return candidates.FirstOrDefault(r => r.RecordKey == item.RecordKey)
                ?? candidates.FirstOrDefault(r => r.Title == item.Title && r.SubTitle == item.SubTitle);
        }
        catch (System.Exception e)
        {
            App.API.LogException(nameof(ResultHelper), $"Failed to query results for {plugin.Metadata.Name}", e);
            return null;
        }
    }

    public static void NavigateToHistoryQuery(LastOpenedHistoryResult item)
    {
        var plugin = PluginManager.GetPluginForId(item.PluginID);
        var queryText = plugin == null
            ? item.Query
            : HistoryReplay.BuildQueryText(item, plugin.Metadata);
        App.API.BackToQueryResults();
        App.API.ChangeQuery(queryText);
    }

    /// <summary>
    /// Right-click on a home-page or history-page entry restores its saved query text.
    /// Applies to both query history and last-opened history. Plugin results are left unchanged
    /// so their context menu still opens.
    /// </summary>
    /// <returns><c>true</c> when the result was a history entry and its text was applied.</returns>
    public static bool TryRestoreHistorySearch(Result? result)
    {
        if (result is not LastOpenedHistoryResult history || !string.IsNullOrEmpty(history.PluginID))
            return false;

        var queryText = !string.IsNullOrEmpty(history.Query)
            ? history.Query
            : history.SourceHistoryItem?.Query ?? string.Empty;
        App.API.BackToQueryResults();
        App.API.ChangeQuery(queryText);
        return true;
    }

    public static async Task<Result?> PopulateResultsAsync(string pluginId, string trimmedQuery, string title, string subTitle, string recordKey)
    {
        var plugin = PluginManager.GetPluginForId(pluginId);
        if (plugin == null) return null;
        var query = QueryBuilder.Build(trimmedQuery, trimmedQuery, PluginManager.GetNonGlobalPlugins());
        if (query == null) return null;
        try
        {
            var freshResults = await PluginManager.QueryForPluginAsync(plugin, query, CancellationToken.None);
            // Try to match by record key first if it is valid, otherwise fall back to title + subtitle match
            if (string.IsNullOrEmpty(recordKey))
            {
                return freshResults?.FirstOrDefault(r => r.Title == title && r.SubTitle == subTitle);
            }
            else
            {
                return freshResults?.FirstOrDefault(r => r.RecordKey == recordKey) ??
                    freshResults?.FirstOrDefault(r => r.Title == title && r.SubTitle == subTitle);
            }
        }
        catch (System.Exception e)
        {
            App.API.LogException(nameof(ResultHelper), $"Failed to query results for {plugin.Metadata.Name}", e);
            return null;
        }
    }
}
