using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.SearchFilters;

public class QueryFilterBarViewModel : BaseModel
{
    private readonly Settings _settings;
    private readonly Func<string> _getQuery;
    private readonly Action<string> _changeQuery;

    public QueryFilterBarViewModel(Settings settings, Func<string> getQuery, Action<string> changeQuery)
    {
        _settings = settings;
        _getQuery = getQuery;
        _changeQuery = changeQuery;
        Filters = new ObservableCollection<QueryFilterItemViewModel>(CreateFilters());

        _settings.PropertyChanged += OnSettingsChanged;
    }

    public ObservableCollection<QueryFilterItemViewModel> Filters { get; }

    public bool IsVisible => _settings.ShowSearchFilterBar;

    public void SyncFromQuery(string queryText)
    {
        var snapshot = QueryFilterSyntax.Parse(queryText);
        foreach (var filter in Filters)
        {
            filter.Sync(snapshot);
        }
    }

    internal void Apply(QueryFilterId id, string value, QueryFilterApplyMode mode)
    {
        var nextQuery = QueryFilterSyntax.Apply(_getQuery() ?? string.Empty, id, value, mode);
        _changeQuery(nextQuery);
    }

    internal void RefreshLabels()
    {
        foreach (var filter in Filters)
        {
            filter.RefreshLabels();
        }
    }

    private void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings.ShowSearchFilterBar))
        {
            OnPropertyChanged(nameof(IsVisible));
        }
        else if (e.PropertyName == nameof(Settings.Language))
        {
            RefreshLabels();
        }
    }

    private QueryFilterItemViewModel[] CreateFilters() =>
    [
        Create(QueryFilterId.Path, "\uED25"),
        Create(QueryFilterId.File, "\uE8A5"),
        Create(QueryFilterId.Folder, "\uE8B7"),
        Create(QueryFilterId.Extension, "\uE71C", QueryFilterCatalog.ExtensionPresets),
        Create(QueryFilterId.NameMatch, "\uE8D2", QueryFilterCatalog.NameMatchPresets),
        Create(QueryFilterId.CaseSensitive, "\uE8D2"),
        Create(QueryFilterId.Size, "\uE9E9"),
        Create(QueryFilterId.DateModified, "\uE823", QueryFilterCatalog.DatePresets),
        Create(QueryFilterId.DateCreated, "\uE787", QueryFilterCatalog.DatePresets),
        Create(QueryFilterId.DateAccessed, "\uE81C", QueryFilterCatalog.DatePresets),
        Create(QueryFilterId.Hidden, "\uE7B3")
    ];

    private QueryFilterItemViewModel Create(QueryFilterId id, string glyph, IReadOnlyList<string> presetValues = null)
    {
        var presets = new ObservableCollection<QueryFilterPresetViewModel>();
        if (presetValues is not null)
        {
            presets.Add(new QueryFilterPresetViewModel(id, string.Empty));
            foreach (var value in presetValues)
            {
                presets.Add(new QueryFilterPresetViewModel(id, value));
            }
        }

        return new QueryFilterItemViewModel(this, id, glyph, presets);
    }
}
