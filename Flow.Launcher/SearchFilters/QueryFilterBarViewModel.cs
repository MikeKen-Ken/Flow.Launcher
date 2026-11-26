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

    public QueryFilterBarViewModel(Settings settings, Func<string> getQuery, Action<string> changeQuery)
    {
        _settings = settings;
        _getQuery = getQuery;
        _changeQuery = changeQuery;
        Filters = new ObservableCollection<QueryFilterItemViewModel>(CreateFilters());
        QuickExtensions = new ObservableCollection<QueryFilterQuickExtensionViewModel>(
            QueryFilterCatalog.PinnedExtensions.Select(extension => new QueryFilterQuickExtensionViewModel(extension)));
        Menu = new QueryFilterMenuState();

        _settings.PropertyChanged += OnSettingsChanged;
    }

    public ObservableCollection<QueryFilterItemViewModel> Filters { get; }

    public ObservableCollection<QueryFilterQuickExtensionViewModel> QuickExtensions { get; }

    public QueryFilterMenuState Menu { get; }

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

        Menu.Sync(HasUnpinnedSelection(snapshot));
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

    private static bool HasUnpinnedSelection(QueryFilterSnapshot snapshot)
    {
        foreach (var pair in snapshot.Values)
        {
            if (pair.Key != QueryFilterId.Extension)
            {
                return true;
            }

            if (QueryFilterExtensionValue.Parse(pair.Value).Any(extension =>
                    !QueryFilterCatalog.PinnedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
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
