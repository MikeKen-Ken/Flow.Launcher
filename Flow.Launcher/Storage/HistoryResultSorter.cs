using System;
using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage;

/// <summary>
/// Prepares last-opened history items for display by applying uniqueness, sort order, and result limits.
/// </summary>
public static class HistoryResultSorter
{
    /// <summary>
    /// Score origin used so history keeps its prepared order after the result list is ranked by <see cref="Result.Score"/>.
    /// Stays below <see cref="Result.MaxScore"/> so user-pinned results remain on top.
    /// </summary>
    internal const int DisplayScoreOrigin = Result.MaxScore / 2;

    /// <summary>
    /// Groups last-opened items when needed, then sorts and optionally limits the list for display.
    /// </summary>
    public static IEnumerable<LastOpenedHistoryResult> Prepare(
        IEnumerable<LastOpenedHistoryResult> historyItems,
        HistoryStyle historyStyle,
        HistorySortOrder sortOrder,
        int? maxResult = null)
    {
        ArgumentNullException.ThrowIfNull(historyItems);

        if (historyStyle == HistoryStyle.LastOpened)
        {
            historyItems = historyItems
                .GroupBy(r => new { r.Title, r.SubTitle, r.PluginID, r.RecordKey })
                .Select(g => g.MaxBy(x => x.ExecutedDateTime)!);
        }

        historyItems = Sort(historyItems, sortOrder);

        if (maxResult.HasValue)
        {
            historyItems = historyItems.Take(maxResult.Value);
        }

        return historyItems;
    }

    /// <summary>
    /// Assigns descending scores so the result view keeps this list order instead of re-ranking by selection count.
    /// </summary>
    public static void AssignDisplayRanking(IList<LastOpenedHistoryResult> displayItems)
    {
        ArgumentNullException.ThrowIfNull(displayItems);

        for (var i = 0; i < displayItems.Count; i++)
        {
            displayItems[i].AddSelectedCount = false;
            displayItems[i].Score = DisplayScoreOrigin - i;
        }
    }

    private static IEnumerable<LastOpenedHistoryResult> Sort(
        IEnumerable<LastOpenedHistoryResult> historyItems,
        HistorySortOrder sortOrder)
    {
        return sortOrder switch
        {
            HistorySortOrder.OldestFirst => historyItems.OrderBy(x => x.ExecutedDateTime),
            HistorySortOrder.TitleAscending => historyItems.OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase),
            HistorySortOrder.TitleDescending => historyItems.OrderByDescending(x => x.Title, StringComparer.CurrentCultureIgnoreCase),
            _ => historyItems.OrderByDescending(x => x.ExecutedDateTime)
        };
    }
}
