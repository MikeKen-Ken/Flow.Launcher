using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Flow.Launcher.SearchFilters;

public partial class QueryFilterBar : UserControl
{
    public QueryFilterBar()
    {
        InitializeComponent();
        SizePickerControl.CloseRequested += OnSizePickerCloseRequested;
        SizePickerPopup.Closed += (_, _) => RestoreQueryBoxFocus();
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
