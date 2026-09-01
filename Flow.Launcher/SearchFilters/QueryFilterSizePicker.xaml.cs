using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Flow.Launcher.SearchFilters;

public partial class QueryFilterSizePicker : UserControl
{
    public QueryFilterSizePicker()
    {
        InitializeComponent();
    }

    public event EventHandler CloseRequested;

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
