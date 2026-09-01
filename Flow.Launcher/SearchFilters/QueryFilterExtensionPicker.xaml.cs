using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Flow.Launcher.SearchFilters;

public partial class QueryFilterExtensionPicker : UserControl
{
    public static readonly DependencyProperty ChipFillProperty = DependencyProperty.Register(
        nameof(ChipFill), typeof(Brush), typeof(QueryFilterExtensionPicker));

    public static readonly DependencyProperty ChipTextProperty = DependencyProperty.Register(
        nameof(ChipText), typeof(Brush), typeof(QueryFilterExtensionPicker));

    public static readonly DependencyProperty ChipStrokeProperty = DependencyProperty.Register(
        nameof(ChipStroke), typeof(Brush), typeof(QueryFilterExtensionPicker));

    public static readonly DependencyProperty ChipSelectedFillProperty = DependencyProperty.Register(
        nameof(ChipSelectedFill), typeof(Brush), typeof(QueryFilterExtensionPicker));

    public static readonly DependencyProperty ChipSelectedTextProperty = DependencyProperty.Register(
        nameof(ChipSelectedText), typeof(Brush), typeof(QueryFilterExtensionPicker));

    public static readonly DependencyProperty PanelFillProperty = DependencyProperty.Register(
        nameof(PanelFill), typeof(Brush), typeof(QueryFilterExtensionPicker));

    public static readonly DependencyProperty PanelStrokeProperty = DependencyProperty.Register(
        nameof(PanelStroke), typeof(Brush), typeof(QueryFilterExtensionPicker));

    private static readonly FontFamily IconFont = new("Segoe MDL2 Assets");
    private readonly List<Button> _tiles = [];
    private QueryFilterItemViewModel _item;

    public QueryFilterExtensionPicker()
    {
        InitializeComponent();
    }

    public Brush ChipFill
    {
        get => (Brush)GetValue(ChipFillProperty);
        set => SetValue(ChipFillProperty, value);
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

    public Brush ChipSelectedFill
    {
        get => (Brush)GetValue(ChipSelectedFillProperty);
        set => SetValue(ChipSelectedFillProperty, value);
    }

    public Brush ChipSelectedText
    {
        get => (Brush)GetValue(ChipSelectedTextProperty);
        set => SetValue(ChipSelectedTextProperty, value);
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

    public event EventHandler CloseRequested;

    internal void ApplyPalette(QueryFilterChipBrushes palette)
    {
        if (palette.Fill is null)
        {
            return;
        }

        ChipFill = palette.Fill;
        ChipText = palette.Text;
        ChipStroke = palette.Stroke;
        ChipSelectedFill = palette.SelectedFill;
        ChipSelectedText = palette.SelectedText;
        PanelFill = palette.PanelFill;
        PanelStroke = palette.PanelStroke;
        RefreshTiles();
    }

    public void Bind(QueryFilterItemViewModel item)
    {
        _item = item;
        AnyButton.Content = Localize.searchFilter_any();
        BuildGroups();
        RefreshTiles();
    }

    private void BuildGroups()
    {
        GroupsHost.Children.Clear();
        _tiles.Clear();
        _tiles.Add(AnyButton);

        foreach (var (key, extensions) in QueryFilterCatalog.ExtensionGroups)
        {
            GroupsHost.Children.Add(new TextBlock
            {
                Text = QueryFilterLabels.ExtensionGroup(key),
                Margin = new Thickness(0, 4, 0, 8),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = ChipText
            });

            var wrap = new WrapPanel();
            foreach (var extension in extensions)
            {
                var check = new TextBlock
                {
                    Text = "\uE73E",
                    FontFamily = IconFont,
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var label = new TextBlock
                {
                    Text = extension,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var content = new StackPanel { Orientation = Orientation.Horizontal };
                content.Children.Add(check);
                content.Children.Add(label);

                var tile = new Button
                {
                    Content = content,
                    Tag = extension,
                    Style = (Style)FindResource("ExtensionTileStyle")
                };
                tile.Click += OnExtensionClick;
                wrap.Children.Add(tile);
                _tiles.Add(tile);
            }

            GroupsHost.Children.Add(wrap);
        }
    }

    private void OnExtensionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string extension } || _item is null)
        {
            return;
        }

        _item.SelectPresetCommand.Execute(extension);
        RefreshTiles();
        Dispatcher.BeginInvoke(RefreshTiles, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnAnyClick(object sender, RoutedEventArgs e)
    {
        _item?.SelectPresetCommand.Execute(string.Empty);
        RefreshTiles();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void RefreshTiles()
    {
        var current = _item?.CurrentValue ?? string.Empty;
        var anySelected = _item is null || !_item.IsSelected;
        ApplyTile(AnyButton, anySelected);
        foreach (var tile in _tiles)
        {
            if (tile.Tag is not string extension)
            {
                continue;
            }

            ApplyTile(tile, QueryFilterExtensionValue.Contains(current, extension));
        }
    }

    private void ApplyTile(Button tile, bool selected)
    {
        tile.Background = selected ? ChipSelectedFill : ChipFill;
        tile.Foreground = selected ? ChipSelectedText : ChipText;
        tile.BorderBrush = selected ? ChipSelectedFill : ChipStroke;

        if (tile.Content is not StackPanel { Children.Count: > 0 } panel)
        {
            return;
        }

        if (panel.Children[0] is TextBlock check)
        {
            check.Foreground = tile.Foreground;
            check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }

        if (panel.Children.Count > 1 && panel.Children[1] is TextBlock label)
        {
            label.Foreground = tile.Foreground;
        }
    }
}
