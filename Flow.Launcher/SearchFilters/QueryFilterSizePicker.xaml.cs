using System;
using System.Windows;
using System.Windows.Controls;
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

    public QueryFilterSizePicker()
    {
        InitializeComponent();
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
        CustomSizeBox.Text = item.CurrentValue ?? string.Empty;
        Dispatcher.BeginInvoke(() =>
        {
            CustomSizeBox.Focus();
            CustomSizeBox.SelectAll();
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void OnPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value } || DataContext is not QueryFilterItemViewModel item)
        {
            return;
        }

        item.SelectPresetCommand.Execute(value);
        CloseRequested?.Invoke(this, EventArgs.Empty);
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
        else if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
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
