using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.History;
using Flow.Launcher.Plugin;
using Flow.Launcher.Storage;

namespace Flow.Launcher.Helper;

#nullable enable

public static class ResultHelper
{
    public static async Task<Result?> PopulateResultsAsync(LastOpenedHistoryResult item)
    {
        var plugin = PluginManager.GetPluginForId(item.PluginID);
        if (plugin == null) return null;

        var query = HistoryReplay.BuildQuery(item, plugin.Metadata)
            ?? QueryBuilder.Build(item.Query, item.Query, PluginManager.GetNonGlobalPlugins());
        if (query == null) return null;

        try
        {
            var freshResults = await PluginManager.QueryForPluginAsync(plugin, query, CancellationToken.None);
            var semanticMatches = freshResults?.Where(r => HistoryReplay.IsSemanticMatch(item, r));

            if (string.IsNullOrEmpty(item.RecordKey))
            {
                return semanticMatches?.FirstOrDefault(r => r.Title == item.Title && r.SubTitle == item.SubTitle);
            }

            return semanticMatches?.FirstOrDefault(r => r.RecordKey == item.RecordKey)
                ?? semanticMatches?.FirstOrDefault(r => r.Title == item.Title && r.SubTitle == item.SubTitle);
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
