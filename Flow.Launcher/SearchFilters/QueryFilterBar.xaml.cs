using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using iNKORE.UI.WPF.Modern;
using Microsoft.Win32;

namespace Flow.Launcher.SearchFilters;

public partial class QueryFilterBar : UserControl
{
    public static readonly DependencyProperty ChipForegroundProperty = DependencyProperty.Register(
        nameof(ChipForeground),
        typeof(Brush),
        typeof(QueryFilterBar),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(245, 245, 245)), OnPaletteSourceChanged));

    public static readonly DependencyProperty ChipFillProperty = DependencyProperty.Register(
        nameof(ChipFill), typeof(Brush), typeof(QueryFilterBar));

    public static readonly DependencyProperty ChipFillHoverProperty = DependencyProperty.Register(
        nameof(ChipFillHover), typeof(Brush), typeof(QueryFilterBar));

    public static readonly DependencyProperty ChipTextProperty = DependencyProperty.Register(
        nameof(ChipText), typeof(Brush), typeof(QueryFilterBar));

    public static readonly DependencyProperty ChipStrokeProperty = DependencyProperty.Register(
        nameof(ChipStroke), typeof(Brush), typeof(QueryFilterBar));

    public static readonly DependencyProperty ChipSelectedFillProperty = DependencyProperty.Register(
        nameof(ChipSelectedFill), typeof(Brush), typeof(QueryFilterBar));

    public static readonly DependencyProperty ChipSelectedTextProperty = DependencyProperty.Register(
        nameof(ChipSelectedText), typeof(Brush), typeof(QueryFilterBar));

    public QueryFilterBar()
    {
        InitializeComponent();
        SizePickerControl.CloseRequested += OnSizePickerCloseRequested;
        SizePickerPopup.Closed += (_, _) => RestoreQueryBoxFocus();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        RefreshPalette();
    }

    public Brush ChipForeground
    {
        get => (Brush)GetValue(ChipForegroundProperty);
        set => SetValue(ChipForegroundProperty, value);
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

    private QueryFilterChipBrushes _palette;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ThemeManager.Current.ActualApplicationThemeChanged += OnThemeChanged;
        RefreshPalette();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ThemeManager.Current.ActualApplicationThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(ThemeManager sender, object args)
    {
        try
        {
            RefreshPalette();
        }
        catch (Exception e)
        {
            App.API.LogError(nameof(QueryFilterBar), $"Failed to refresh filter chip palette: {e.Message}");
        }
    }

    private static void OnPaletteSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is QueryFilterBar bar)
        {
            bar.RefreshPalette();
        }
    }

    private void RefreshPalette()
    {
        var text = BrushColor(ChipForeground, Color.FromRgb(245, 245, 245));
        var surface = FindSurfaceColor();
        var accent = BrushColor(
            TryFindResource("SystemAccentColorLight1Brush") as Brush
            ?? TryFindResource("BasicSystemAccentColor") as Brush,
            QueryFilterChipPalette.FallbackAccentColor);

        _palette = QueryFilterChipPalette.Create(text, surface, accent);
        ChipFill = _palette.Fill;
        ChipFillHover = _palette.FillHover;
        ChipText = _palette.Text;
        ChipStroke = _palette.Stroke;
        ChipSelectedFill = _palette.SelectedFill;
        ChipSelectedText = _palette.SelectedText;
        SizePickerControl?.ApplyPalette(_palette);
    }

    private Color FindSurfaceColor()
    {
        DependencyObject current = this;
        while (current is not null)
        {
            if (current is Border { Background: SolidColorBrush { Color.A: > 24 } solid })
            {
                return solid.Color;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        if (TryFindResource("Color01B") is SolidColorBrush chrome)
        {
            return chrome.Color;
        }

        return QueryFilterChipPalette.FallbackSurface;
    }

    private static Color BrushColor(Brush brush, Color fallback) =>
        brush is SolidColorBrush solid ? solid.Color : fallback;

    private void OnFilterChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not QueryFilterItemViewModel item)
        {
            return;
        }

        if (item.UsesSizePicker)
        {
            OpenSizePicker(button, item);
            return;
        }

        if (item.UsesFolderPicker)
        {
            if (item.IsSelected)
            {
                ShowPathMenu(button, item);
            }
            else
            {
                OpenFolderPicker(item);
            }

            return;
        }

        if (item.UsesMultiSelect)
        {
            ShowExtensionMenu(button, item);
            return;
        }

        if (!item.HasPresets)
        {
            item.ActivateCommand.Execute(null);
            RestoreQueryBoxFocus();
            return;
        }

        var menu = new ContextMenu
        {
            PlacementTarget = button,
            Placement = PlacementMode.Bottom
        };

        foreach (var preset in item.Presets)
        {
            menu.Items.Add(new MenuItem
            {
                Header = preset.Label,
                IsChecked = preset.IsSelected,
                Command = item.SelectPresetCommand,
                CommandParameter = preset.Value
            });
        }

        menu.Closed += (_, _) => RestoreQueryBoxFocus();
        menu.IsOpen = true;
    }

    private void ShowExtensionMenu(Button button, QueryFilterItemViewModel item)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = button,
            Placement = PlacementMode.Bottom
        };

        var anyItem = new MenuItem
        {
            Header = Localize.searchFilter_any(),
            IsChecked = !item.IsSelected,
            Command = item.SelectPresetCommand,
            CommandParameter = string.Empty
        };
        menu.Items.Add(anyItem);

        foreach (var (key, extensions) in QueryFilterCatalog.ExtensionGroups)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem
            {
                Header = QueryFilterLabels.ExtensionGroup(key),
                IsEnabled = false
            });

            foreach (var extension in extensions)
            {
                var preset = new MenuItem
                {
                    Header = extension,
                    IsCheckable = true,
                    StaysOpenOnClick = true,
                    IsChecked = QueryFilterExtensionValue.Contains(item.CurrentValue, extension),
                    Command = item.SelectPresetCommand,
                    CommandParameter = extension
                };
                preset.Click += (_, _) => RefreshExtensionMenu(menu, item);
                menu.Items.Add(preset);
            }
        }

        menu.Closed += (_, _) => RestoreQueryBoxFocus();
        menu.IsOpen = true;
    }

    private static void RefreshExtensionMenu(ContextMenu menu, QueryFilterItemViewModel item)
    {
        foreach (var menuItem in menu.Items.OfType<MenuItem>())
        {
            if (menuItem.CommandParameter is not string value)
            {
                continue;
            }

            menuItem.IsChecked = string.IsNullOrEmpty(value)
                ? !item.IsSelected
                : QueryFilterExtensionValue.Contains(item.CurrentValue, value);
        }
    }

    private void OpenSizePicker(Button button, QueryFilterItemViewModel item)
    {
        SizePickerControl.ApplyPalette(_palette);
        SizePickerControl.Bind(item);
        SizePickerPopup.PlacementTarget = button;
        SizePickerPopup.IsOpen = true;
    }

    private void ShowPathMenu(Button button, QueryFilterItemViewModel item)
    {
        var pickAfterClose = false;
        var menu = new ContextMenu
        {
            PlacementTarget = button,
            Placement = PlacementMode.Bottom
        };

        var changeItem = new MenuItem
        {
            Header = Localize.searchFilter_path_change()
        };
        changeItem.Click += (_, _) => pickAfterClose = true;
        menu.Items.Add(changeItem);
        menu.Items.Add(new MenuItem
        {
            Header = Localize.searchFilter_any(),
            Command = item.SetPathCommand,
            CommandParameter = string.Empty
        });

        menu.Closed += (_, _) =>
        {
            if (pickAfterClose)
            {
                OpenFolderPicker(item);
            }
            else
            {
                RestoreQueryBoxFocus();
            }
        };
        menu.IsOpen = true;
    }

    private void OpenFolderPicker(QueryFilterItemViewModel item)
    {
        var owner = Window.GetWindow(this);
        var wasTopmost = owner?.Topmost ?? false;
        if (owner is not null)
        {
            owner.Topmost = false;
        }

        try
        {
            var dialog = new OpenFolderDialog
            {
                Title = Localize.searchFilter_path_pick(),
                Multiselect = false
            };

            if (!string.IsNullOrEmpty(item.CurrentValue) && Directory.Exists(item.CurrentValue))
            {
                dialog.InitialDirectory = item.CurrentValue;
            }

            var confirmed = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
            if (confirmed == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                item.SetPathCommand.Execute(dialog.FolderName);
            }
        }
        finally
        {
            if (owner is not null)
            {
                owner.Topmost = wasTopmost;
            }

            RestoreQueryBoxFocus();
        }
    }

    private void OnSizePickerCloseRequested(object sender, EventArgs e)
    {
        SizePickerPopup.IsOpen = false;
        RestoreQueryBoxFocus();
    }

    private void RestoreQueryBoxFocus()
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            Keyboard.Focus(mainWindow.QueryTextBox);
        }
    }
}
