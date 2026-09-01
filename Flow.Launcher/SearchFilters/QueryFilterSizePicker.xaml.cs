using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Flow.Launcher.SearchFilters;

public partial class QueryFilterSizePicker : UserControl
{
    public static readonly DependencyProperty ChipFillProperty = DependencyProperty.Register(
        nameof(ChipFill), typeof(Brush), typeof(QueryFilterSizePicker));

    public static readonly DependencyProperty ChipFillHoverProperty = DependencyProperty.Register(
        nameof(ChipFillHover), typeof(Brush), typeof(QueryFilterSizePicker));

    public static readonly DependencyProperty ChipTextProperty = DependencyProperty.Register(
        nameof(ChipText), typeof(Brush), typeof(QueryFilterSizePicker));

    public static readonly DependencyProperty ChipStrokeProperty = DependencyProperty.Register(
        nameof(ChipStroke), typeof(Brush), typeof(QueryFilterSizePicker));

    public static readonly DependencyProperty PanelFillProperty = DependencyProperty.Register(
        nameof(PanelFill), typeof(Brush), typeof(QueryFilterSizePicker));

    public static readonly DependencyProperty PanelStrokeProperty = DependencyProperty.Register(
        nameof(PanelStroke), typeof(Brush), typeof(QueryFilterSizePicker));

    public static readonly DependencyProperty FieldFillProperty = DependencyProperty.Register(
        nameof(FieldFill), typeof(Brush), typeof(QueryFilterSizePicker));

    private bool _dragging;
    private bool _suppress;

    public QueryFilterSizePicker()
    {
        _suppress = true;
        InitializeComponent();
        MinSlider.Maximum = QueryFilterSizeSteps.LastIndex;
        MaxSlider.Maximum = QueryFilterSizeSteps.LastIndex;
        _suppress = false;
    }

    public Brush ChipFill
    {
        get => (Brush)GetValue(ChipFillProperty);
        set => SetValue(ChipFillProperty, value);
    }

    public Brush ChipFillHover
    {
        get => (Brush)GetValue(ChipFillHoverProperty);
        set => SetValue(ChipFillHoverProperty, value);
    }

    public Brush ChipText
    {
        get => (Brush)GetValue(ChipTextProperty);
        set => SetValue(ChipTextProperty, value);
    }

    public Brush ChipStroke
    {
        get => (Brush)GetValue(ChipStrokeProperty);
        set => SetValue(ChipStrokeProperty, value);
    }

    public Brush PanelFill
    {
        get => (Brush)GetValue(PanelFillProperty);
        set => SetValue(PanelFillProperty, value);
    }

    public Brush PanelStroke
    {
        get => (Brush)GetValue(PanelStrokeProperty);
        set => SetValue(PanelStrokeProperty, value);
    }

    public Brush FieldFill
    {
        get => (Brush)GetValue(FieldFillProperty);
        set => SetValue(FieldFillProperty, value);
    }

    public event EventHandler CloseRequested;

    internal void ApplyPalette(QueryFilterChipBrushes palette)
    {
        if (palette.Fill is null)
        {
            return;
        }

        ChipFill = palette.Fill;
        ChipFillHover = palette.FillHover;
        ChipText = palette.Text;
        ChipStroke = palette.Stroke;
        PanelFill = palette.PanelFill;
        PanelStroke = palette.PanelStroke;
        FieldFill = palette.FieldFill;
    }

    public void Bind(QueryFilterItemViewModel item)
    {
        DataContext = item;
        _suppress = true;
        try
        {
            CustomSizeBox.Text = item.CurrentValue ?? string.Empty;
            if (QueryFilterSizeValue.TryParseBounds(item.CurrentValue, out var min, out var max))
            {
                MinSlider.Value = QueryFilterSizeSteps.IndexOf(min);
                MaxSlider.Value = QueryFilterSizeSteps.IndexOf(max);
            }
            else
            {
                MinSlider.Value = QueryFilterSizeSteps.AnyIndex;
                MaxSlider.Value = QueryFilterSizeSteps.AnyIndex;
            }

            UpdateBoundLabels();
        }
        finally
        {
            _suppress = false;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void OnSliderDragStarted(object sender, DragStartedEventArgs e)
    {
        _dragging = true;
    }

    private void OnSliderDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _dragging = false;
        ApplyFromSliders();
    }

    private void OnMinSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        OnBoundSliderChanged(changingMin: true);
    }

    private void OnMaxSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        OnBoundSliderChanged(changingMin: false);
    }

    private void OnApplyCustomClick(object sender, RoutedEventArgs e)
    {
        ApplyCustom();
    }

    private void OnCustomSizePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyCustom();
            e.Handled = true;
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is QueryFilterItemViewModel item)
        {
            item.SetSizeCommand.Execute(string.Empty);
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnBoundSliderChanged(bool changingMin)
    {
        if (_suppress)
        {
            return;
        }

        EnsureOrder(changingMin);
        UpdateBoundLabels();
        if (!_dragging)
        {
            ApplyFromSliders();
        }
    }

    private void EnsureOrder(bool changingMin)
    {
        var minIndex = (int)MinSlider.Value;
        var maxIndex = (int)MaxSlider.Value;
        if (minIndex <= QueryFilterSizeSteps.AnyIndex || maxIndex <= QueryFilterSizeSteps.AnyIndex || minIndex < maxIndex)
        {
            return;
        }

        _suppress = true;
        try
        {
            if (changingMin)
            {
                MaxSlider.Value = minIndex >= QueryFilterSizeSteps.LastIndex
                    ? QueryFilterSizeSteps.AnyIndex
                    : minIndex + 1;
            }
            else
            {
                MinSlider.Value = maxIndex <= 1
                    ? QueryFilterSizeSteps.AnyIndex
                    : maxIndex - 1;
            }
        }
        finally
        {
            _suppress = false;
        }
    }

    private void UpdateBoundLabels()
    {
        MinValueText.Text = BoundLabel((int)MinSlider.Value, greaterThan: true);
        MaxValueText.Text = BoundLabel((int)MaxSlider.Value, greaterThan: false);
    }

    private static string BoundLabel(int index, bool greaterThan)
    {
        var token = QueryFilterSizeSteps.TokenAt(index);
        if (string.IsNullOrEmpty(token))
        {
            return Localize.searchFilter_any();
        }

        var pretty = QueryFilterSizeValue.ToDisplay(token);
        return greaterThan ? ">" + pretty : "<" + pretty;
    }

    private void ApplyFromSliders()
    {
        if (DataContext is not QueryFilterItemViewModel item)
        {
            return;
        }

        var value = QueryFilterSizeValue.FormatBounds(
            QueryFilterSizeSteps.TokenAt((int)MinSlider.Value),
            QueryFilterSizeSteps.TokenAt((int)MaxSlider.Value));
        CustomSizeBox.Text = string.IsNullOrEmpty(value) ? string.Empty : QueryFilterSizeValue.ToDisplay(value);
        item.SetSizeCommand.Execute(value);
    }

    private void ApplyCustom()
    {
        if (DataContext is not QueryFilterItemViewModel item)
        {
            return;
        }

        var text = CustomSizeBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            item.SetSizeCommand.Execute(string.Empty);
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (!QueryFilterSizeValue.TryNormalize(text, out var value))
        {
            return;
        }

        item.SetSizeCommand.Execute(value);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
