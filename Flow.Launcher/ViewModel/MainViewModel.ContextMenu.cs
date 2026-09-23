using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel;

public partial class MainViewModel
{
    private Task<List<Result>> _contextMenuLoad;
    private Result _contextMenuSource;
    private List<Result> _contextMenuActions;
    private int _contextMenuQueryVersion;

    private void ResetContextMenuSession()
    {
        _contextMenuQueryVersion++;
        _contextMenuLoad = null;
        _contextMenuSource = null;
        _contextMenuActions = null;
    }

    private async Task QueryContextMenuAsync()
    {
        const string id = "Context Menu ID";
        var selected = Results.SelectedItem?.Result;
        if (selected == null || string.IsNullOrEmpty(selected.PluginID))
        {
            ResetContextMenuSession();
            ContextMenu.Clear();
            ContextMenu.SelectedItem = null;
            return;
        }

        if (!ReferenceEquals(_contextMenuSource, selected))
            ResetContextMenuSession();

        var version = ++_contextMenuQueryVersion;
        var query = QueryText.ToLower().Trim();
        try
        {
            if (_contextMenuLoad == null)
            {
                _contextMenuSource = selected;
                // Shell extensions require STA. Loading on the dispatcher freezes
                // input and painting while native menus or external plugins respond.
                _contextMenuLoad = Win32Helper.StartSTATaskAsync(() => PluginManager.GetContextMenusForPlugin(selected));
                ContextMenu.Clear();
                ContextMenu.AddResults(new List<Result>
                {
                    new() { Title = Localize.pleaseWait(), Action = _ => false }
                }, id);
            }

            var loaded = await _contextMenuLoad;
            // Navigation and newer filter queries supersede this continuation.
            if (version != _contextMenuQueryVersion || !ContextMenuSelected() ||
                !ReferenceEquals(selected, Results.SelectedItem?.Result))
                return;

            _contextMenuActions ??= new List<Result>(loaded)
            {
                ContextMenuTopMost(selected),
                ContextMenuPluginSettings(selected),
                ContextMenuPluginInfo(selected)
            };

            var results = _contextMenuActions;
            if (!string.IsNullOrEmpty(query))
            {
                results = results.Select(x => x.Clone()).Where(r =>
                {
                    var match = App.API.FuzzySearch(query, r.Title);
                    if (!match.IsSearchPrecisionScoreMet())
                        match = App.API.FuzzySearch(query, r.SubTitle);
                    if (!match.IsSearchPrecisionScoreMet()) return false;
                    r.Score = match.Score;
                    return true;
                }).ToList();
            }

            ContextMenu.Clear();
            ContextMenu.SelectedItem = null;
            ContextMenu.AddResults(results, id);
        }
        catch (Exception exception)
        {
            if (version != _contextMenuQueryVersion || !ContextMenuSelected())
                return;
            ResetContextMenuSession();
            ContextMenu.Clear();
            ContextMenu.SelectedItem = null;
            App.API.LogException(ClassName, "Unable to load context menu", exception);
        }
    }
}
