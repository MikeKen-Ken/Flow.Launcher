using System.Globalization;
using Flow.Launcher.Plugin;
using Flow.Launcher.Storage;

namespace Flow.Launcher.History;

/// <summary>
/// Provides the complete presentation model for a recent-action history result.
/// </summary>
public sealed record HistoryPresentation(
    string PluginName,
    string ActionLabel,
    HistoryActionKind ActionKind,
    string Command,
    string ExecutedTime,
    string ExecutedTimeToolTip)
{
    public static HistoryPresentation From(LastOpenedHistoryResult result)
    {
        if (!result.UseProvenancePresentation)
        {
            return null;
        }

        var provenance = result.Provenance;
        var pluginName = provenance?.PluginName;
        if (string.IsNullOrWhiteSpace(pluginName))
        {
            pluginName = result.PluginID;
        }

        return new HistoryPresentation(
            pluginName ?? string.Empty,
            provenance?.ActionLabel ?? string.Empty,
            provenance?.ActionKind ?? HistoryActionKind.Unknown,
            result.Query,
            result.ExecutedDateTime.ToString("g", CultureInfo.CurrentCulture),
            Localize.lastExecuteTime(result.ExecutedDateTime));
    }
}
