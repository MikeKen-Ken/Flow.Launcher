using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.SearchFilters;

public partial class QueryFilterItemViewModel : BaseModel
{
    private readonly QueryFilterBarViewModel _owner;

    internal QueryFilterItemViewModel(
        QueryFilterBarViewModel owner,
        QueryFilterId id,
        string glyph,
        ObservableCollection<QueryFilterPresetViewModel> presets)
    {
        _owner = owner;
        Id = id;
        Glyph = glyph;
        Presets = presets;
        HasPresets = presets.Count > 0;
        UsesSizePicker = id == QueryFilterId.Size;
        UsesFolderPicker = id == QueryFilterId.Path;
        ShowsMenu = HasPresets || UsesSizePicker || UsesFolderPicker;
        // Labels are applied after language init. Localize uses PublicApi.Instance,
        // which deadlocks if called while IPublicAPI is still being resolved.
    }

    internal QueryFilterId Id { get; }

    public string Glyph { get; }

    public bool HasPresets { get; }

    public bool UsesSizePicker { get; }

    public bool UsesFolderPicker { get; }

    public bool ShowsMenu { get; }

    public ObservableCollection<QueryFilterPresetViewModel> Presets { get; }

    public string Label { get; private set; } = string.Empty;

    public string DisplayText { get; private set; } = string.Empty;

    public string Tooltip { get; private set; } = string.Empty;

    public bool IsSelected { get; private set; }

    public string CurrentValue { get; private set; } = string.Empty;

    internal void RefreshLabels()
    {
        Label = QueryFilterLabels.Chip(Id);
        Tooltip = QueryFilterLabels.Tooltip(Id, CurrentValue);
        DisplayText = QueryFilterLabels.Display(Id, Label, CurrentValue, IsSelected);
        foreach (var preset in Presets)
        {
            preset.RefreshLabel();
        }
    }

    internal void Sync(QueryFilterSnapshot snapshot)
    {
        IsSelected = snapshot.IsActive(Id);
        CurrentValue = snapshot.GetValue(Id);
        Tooltip = QueryFilterLabels.Tooltip(Id, CurrentValue);
        DisplayText = QueryFilterLabels.Display(Id, Label, CurrentValue, IsSelected);

        foreach (var preset in Presets)
        {
            preset.IsSelected = IsSelected &&
                string.Equals(preset.Value, CurrentValue, System.StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand]
    private void Activate()
    {
        _owner.Apply(Id, string.Empty, QueryFilterApplyMode.Toggle);
    }

    [RelayCommand]
    private void SelectPreset(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            _owner.Apply(Id, string.Empty, QueryFilterApplyMode.Clear);
            return;
        }

        _owner.Apply(Id, value, QueryFilterApplyMode.Toggle);
    }

    [RelayCommand]
    private void SetSize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _owner.Apply(Id, string.Empty, QueryFilterApplyMode.Clear);
            return;
        }

        _owner.Apply(Id, value, QueryFilterApplyMode.Set);
    }

    [RelayCommand]
    private void SetPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _owner.Apply(Id, string.Empty, QueryFilterApplyMode.Clear);
            return;
        }

        _owner.Apply(Id, value, QueryFilterApplyMode.Set);
    }
}

public class QueryFilterPresetViewModel : BaseModel
{
    internal QueryFilterPresetViewModel(QueryFilterId filterId, string value)
    {
        FilterId = filterId;
        Value = value;
    }

    internal QueryFilterId FilterId { get; }

    public string Value { get; }

    public string Label { get; private set; } = string.Empty;

    public bool IsSelected { get; set; }

    internal void RefreshLabel()
    {
        Label = string.IsNullOrEmpty(Value)
            ? Localize.searchFilter_any()
            : QueryFilterLabels.Preset(FilterId, Value);
    }
}
