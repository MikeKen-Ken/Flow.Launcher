using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Core.WebDavSync;

namespace Flow.Launcher;

public partial class WebDavSyncConfirmWindow
{
    public WebDavSyncConfirmWindow(WebDavSyncOperation operation)
    {
        InitializeComponent();
        TitleTextBlock.Text = GetTitle(operation);
        MessageTextBlock.Text = GetMessage(operation);
        Loaded += (_, _) => CancelButton.Focus();
    }

    public static bool Confirm(Window owner, WebDavSyncOperation operation)
    {
        var window = new WebDavSyncConfirmWindow(operation);
        if (owner != null && owner.IsVisible)
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return window.ShowDialog() == true;
    }

    private static string GetTitle(WebDavSyncOperation operation) => operation switch
    {
        WebDavSyncOperation.Upload => Localize.webDavSyncConfirmTitleUpload(),
        WebDavSyncOperation.Download => Localize.webDavSyncConfirmTitleDownload(),
        _ => Localize.webDavSyncConfirmTitleSync()
    };

    private static string GetMessage(WebDavSyncOperation operation) => operation switch
    {
        WebDavSyncOperation.Upload => Localize.webDavSyncConfirmMessageUpload(System.Environment.NewLine),
        WebDavSyncOperation.Download => Localize.webDavSyncConfirmMessageDownload(System.Environment.NewLine),
        _ => Localize.webDavSyncConfirmMessageSync(System.Environment.NewLine)
    };

    private void OnConfirmClicked(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        DialogResult = false;
        Close();
    }

    private void OnCancelExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        e.Handled = true;
        DialogResult = false;
        Close();
    }
}
