using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace Flow.Launcher.SearchFilters;

public partial class QueryFilterBar : UserControl
{
    public static readonly DependencyProperty ChipForegroundProperty = DependencyProperty.Register(
        nameof(ChipForeground),
        typeof(Brush),
        typeof(QueryFilterBar),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(245, 245, 245))));

    public QueryFilterBar()
    {
        InitializeComponent();
        SizePickerControl.CloseRequested += OnSizePickerCloseRequested;
        SizePickerPopup.Closed += (_, _) => RestoreQueryBoxFocus();
    }

    public Brush ChipForeground
    {
        get => (Brush)GetValue(ChipForegroundProperty);
        set => SetValue(ChipForegroundProperty, value);
    }

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
