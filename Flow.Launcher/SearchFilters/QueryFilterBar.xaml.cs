using System;
using System.IO;
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
        SizePickerControl.CloseRequested += OnPickerCloseRequested;
        ExtensionPickerControl.CloseRequested += OnPickerCloseRequested;
        SizePickerPopup.Closed += (_, _) => RestoreQueryBoxFocus();
        ExtensionPickerPopup.Closed += (_, _) => OnExtensionPickerClosed();
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
        HookExtensionOutsideClick(false);
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
        ExtensionPickerControl?.ApplyPalette(_palette);
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
            OpenExtensionPicker(button, item);
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

    private void OpenSizePicker(Button button, QueryFilterItemViewModel item)
    {
        SizePickerPopup.IsOpen = false;
        ExtensionPickerPopup.IsOpen = false;
        SizePickerControl.ApplyPalette(_palette);
        SizePickerControl.Bind(item);
        SizePickerPopup.PlacementTarget = button;
        SizePickerPopup.IsOpen = true;
    }

    private void OpenExtensionPicker(Button button, QueryFilterItemViewModel item)
    {
        SizePickerPopup.IsOpen = false;
        if (ExtensionPickerPopup.IsOpen)
        {
            ExtensionPickerPopup.IsOpen = false;
        }

        ExtensionPickerControl.ApplyPalette(_palette);
        ExtensionPickerControl.Bind(item);
        ExtensionPickerPopup.PlacementTarget = button;
        ExtensionPickerPopup.IsOpen = true;
        HookExtensionOutsideClick(true);
    }

    private void HookExtensionOutsideClick(bool enable)
    {
        if (Window.GetWindow(this) is not Window window)
        {
            return;
        }

        window.PreviewMouseDown -= OnWindowPreviewMouseDownForExtension;
        if (enable)
        {
            window.PreviewMouseDown += OnWindowPreviewMouseDownForExtension;
        }
    }

    private void OnWindowPreviewMouseDownForExtension(object sender, MouseButtonEventArgs e)
    {
        if (!ExtensionPickerPopup.IsOpen || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (IsInside(ExtensionPickerControl, source))
        {
            return;
        }

        ExtensionPickerPopup.IsOpen = false;
    }

    private void OnExtensionPickerClosed()
    {
        HookExtensionOutsideClick(false);
        RestoreQueryBoxFocus();
    }

    private static bool IsInside(DependencyObject root, DependencyObject source)
    {
        while (source is not null)
        {
            if (ReferenceEquals(source, root))
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }

        return false;
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

    private void OnPickerCloseRequested(object sender, EventArgs e)
    {
        SizePickerPopup.IsOpen = false;
        ExtensionPickerPopup.IsOpen = false;
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
