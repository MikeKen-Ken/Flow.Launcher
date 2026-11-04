using System;
using System.Collections.Generic;
using System.Linq;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.History;

/// <summary>
/// Builds a history record for a context-menu command and finds that command again later.
/// The stored row keeps the source result's identity and adds the menu command as its action.
/// </summary>
public static class ContextMenuHistory
{
    public const string ActionIdPrefix = "context-menu:";

    /// <summary>
    /// Creates the result passed to history storage.
    /// Returns null for host-only menu rows, which have no plugin id.
    /// </summary>
    public static Result TryCreate(Result source, Result menuItem)
    {
        if (source == null || menuItem == null)
            return null;

        if (string.IsNullOrEmpty(source.PluginID) || string.IsNullOrEmpty(menuItem.PluginID))
            return null;

        if (string.IsNullOrEmpty(source.OriginQuery?.TrimmedQuery) || string.IsNullOrEmpty(menuItem.Title))
            return null;

        return new Result
        {
            Title = source.Title,
            SubTitle = source.SubTitle,
            PluginID = source.PluginID,
            OriginQuery = source.OriginQuery,
            RecordKey = source.RecordKey,
            IcoPath = source.IcoPath,
            Glyph = source.Glyph,
            PluginDirectory = source.PluginDirectory,
            HistoryAction = Describe(menuItem)
        };
    }

    /// <summary>
    /// Finds the menu row that matches a saved context-menu action.
    /// </summary>
    public static Result FindMenuItem(IReadOnlyList<Result> menuItems, HistoryProvenance provenance)
    {
        if (menuItems == null || menuItems.Count == 0 || provenance == null)
            return null;

        var actionId = provenance.ActionId ?? string.Empty;
        if (!string.IsNullOrEmpty(actionId))
        {
            var byPluginId = menuItems.FirstOrDefault(item =>
                string.Equals(item.HistoryAction?.Id, actionId, StringComparison.Ordinal));
            if (byPluginId != null)
                return byPluginId;

            if (actionId.StartsWith(ActionIdPrefix, StringComparison.Ordinal))
            {
                var title = actionId[ActionIdPrefix.Length..];
                var byTitle = menuItems.FirstOrDefault(item =>
                    string.Equals(item.Title, title, StringComparison.Ordinal));
                if (byTitle != null)
                    return byTitle;
            }
        }

        var label = provenance.ActionLabel ?? string.Empty;
        if (string.IsNullOrEmpty(label))
            return null;

        return menuItems.FirstOrDefault(item =>
            string.Equals(item.Title, label, StringComparison.Ordinal));
    }

    private static HistoryActionDescriptor Describe(Result menuItem)
    {
        var described = menuItem.HistoryAction;
        if (described != null && !string.IsNullOrEmpty(described.Id))
        {
            if (!string.IsNullOrEmpty(described.Label))
                return described;

            return described with { Label = menuItem.Title };
        }

        return new HistoryActionDescriptor
        {
            Id = ActionIdPrefix + menuItem.Title,
            Label = string.IsNullOrEmpty(described?.Label) ? menuItem.Title : described.Label,
            Kind = described?.Kind ?? HistoryActionKind.Execute,
            ReplayMode = described?.ReplayMode ?? HistoryReplayMode.Execute
        };
    }
}
