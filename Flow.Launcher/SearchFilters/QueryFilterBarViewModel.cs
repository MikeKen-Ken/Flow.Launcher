using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.SearchFilters;

public class QueryFilterBarViewModel : BaseModel
{
    private readonly Settings _settings;
    private readonly Func<string> _getQuery;
    private readonly Action<string> _changeQuery;

    private readonly Action _toggleHistorySearch;

    public QueryFilterBarViewModel(
        Settings settings,
        Func<string> getQuery,
        Action<string> changeQuery,
        Action toggleHistorySearch = null)
    {
        _settings = settings;
        _getQuery = getQuery;
        _changeQuery = changeQuery;
        _toggleHistorySearch = toggleHistorySearch;
        var filters = CreateFilters();
        Filters = new ObservableCollection<QueryFilterItemViewModel>(filters);
        PanelFilters = new ObservableCollection<QueryFilterItemViewModel>(filters.Where(filter => IsInPanel(filter.Id)));
        QuickExtensions = new ObservableCollection<QueryFilterQuickExtensionViewModel>(
            QueryFilterCatalog.PinnedExtensions.Select(extension => new QueryFilterQuickExtensionViewModel(extension)));
        Menu = new QueryFilterMenuState();
        HistorySearch = new QueryFilterHistorySearchState();
        BarItems = BuildBarItems(filters, QuickExtensions, HistorySearch, Menu);

        _settings.PropertyChanged += OnSettingsChanged;
    }

    public ObservableCollection<QueryFilterItemViewModel> Filters { get; }

    public ObservableCollection<QueryFilterItemViewModel> PanelFilters { get; }

    public IReadOnlyList<object> BarItems { get; }

    public ObservableCollection<QueryFilterQuickExtensionViewModel> QuickExtensions { get; }

    public QueryFilterMenuState Menu { get; }

    public QueryFilterHistorySearchState HistorySearch { get; }

    public void SyncHistorySearch(bool active) => HistorySearch.Sync(active);

    internal void ToggleHistorySearch() => _toggleHistorySearch?.Invoke();

    public bool IsVisible => _settings.ShowSearchFilterBar;

    public void SyncFromQuery(string queryText)
    {
        var snapshot = QueryFilterSyntax.Parse(queryText);
        foreach (var filter in Filters)
        {
            filter.Sync(snapshot);
        }

        SyncQuickSelection(snapshot);
    }

    internal void SyncQuickSelection(string queryText)
    {
        SyncQuickSelection(QueryFilterSyntax.Parse(queryText));
    }

    private void SyncQuickSelection(QueryFilterSnapshot snapshot)
    {
        var extensions = snapshot.GetValue(QueryFilterId.Extension);
        foreach (var quick in QuickExtensions)
        {
            quick.Sync(extensions);
        }

        Menu.Sync(QuickExtensions.Any(item => item.IsSelected)
            || PanelFilters.Any(filter => snapshot.IsActive(filter.Id)));
    }

    private static bool IsInPanel(QueryFilterId id) =>
        id is QueryFilterId.Size or QueryFilterId.DateModified or QueryFilterId.DateCreated
            or QueryFilterId.DateAccessed or QueryFilterId.Hidden;

    private static IReadOnlyList<object> BuildBarItems(
        QueryFilterItemViewModel[] filters,
        IEnumerable<QueryFilterQuickExtensionViewModel> quickExtensions,
        QueryFilterHistorySearchState historySearch,
        QueryFilterMenuState menu)
    {
        var items = new List<object> { historySearch };
        foreach (var filter in filters)
        {
            if (IsInPanel(filter.Id))
            {
                continue;
            }

            items.Add(filter);
            if (filter.Id == QueryFilterId.Folder)
            {
                items.AddRange(quickExtensions);
            }
        }

        items.Add(menu);
        return items;
    }

    internal void ToggleQuickExtension(string extension)
    {
        Apply(QueryFilterId.Extension, extension, QueryFilterApplyMode.Toggle);
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

        foreach (var quick in QuickExtensions)
        {
            quick.RefreshLabel();
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

public sealed class QueryFilterHistorySearchState : BaseModel
{
    public bool IsSelected { get; private set; }

    internal void Sync(bool active)
    {
        IsSelected = active;
    }
}

public sealed class QueryFilterMenuState : BaseModel
{
    public bool IsSelected { get; private set; }

    internal void Sync(bool active)
    {
        IsSelected = active;
    }
}

public sealed class QueryFilterQuickExtensionViewModel : BaseModel
{
    public QueryFilterQuickExtensionViewModel(string extension)
    {
        Extension = extension;
        Label = "." + extension;
        Tooltip = "ext:" + extension;
    }

    public string Extension { get; }

    public string Label { get; }

    public bool IsSelected { get; private set; }

    public string Tooltip { get; private set; }

    internal void Sync(string currentExtensions)
    {
        IsSelected = QueryFilterExtensionValue.Contains(currentExtensions, Extension);
    }

    internal void RefreshLabel()
    {
        Tooltip = Localize.searchFilter_tooltip("ext:" + Extension);
    }
}
