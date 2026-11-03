using System.Threading;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.ShellFolderSearch;

/// <summary>
/// Applies a folder-scoped search to the main window, waiting for plugins when needed
/// so Explorer can handle the path query.
/// </summary>
public static class FolderSearchInvoker
{
    private static readonly Lock SyncRoot = new();
    private static string _pendingFolderPath;
    private static bool _pluginsReady;
    private static bool _holdingWindowOpen;

    public static bool HasPending
    {
        get
        {
            lock (SyncRoot)
            {
                return _pendingFolderPath != null;
            }
        }
    }

    public static void CaptureFromCommandLine(string[] args)
    {
        if (FolderSearchCommand.TryGetFolder(args, out var folderPath))
            Request(folderPath);
    }

    public static void Request(string folderPath)
    {
        if (!FolderSearchCommand.TryNormalizeFolderPath(folderPath, out var normalized))
            return;

        string toApply = null;
        lock (SyncRoot)
        {
            if (_pluginsReady)
                toApply = normalized;
            else
                _pendingFolderPath = normalized;
        }

        if (toApply != null)
            Apply(toApply);
    }

    public static void NotifyPluginsReady()
    {
        string toApply;
        lock (SyncRoot)
        {
            _pluginsReady = true;
            toApply = _pendingFolderPath;
            _pendingFolderPath = null;
        }

        if (toApply != null)
            Apply(toApply);
    }

    /// <summary>
    /// Shows the pending folder search before hotkey registration steals focus.
    /// The hold stays until <see cref="NotifyPluginsReady"/> applies the query.
    /// </summary>
    public static void ShowPendingShell()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
            return;

        dispatcher.Invoke(() =>
        {
            BeginHoldWindowOpen();
            App.API.ShowMainWindow();
            BringToForeground();
        });
    }

    private static void Apply(string folderPath)
    {
        var query = FolderSearchCommand.BuildQuery(folderPath);
        var dispatcher = Application.Current?.Dispatcher;
        if (string.IsNullOrEmpty(query) || dispatcher == null)
        {
            EndHoldWindowOpen();
            return;
        }

        dispatcher.Invoke(() =>
        {
            BeginHoldWindowOpen();
            try
            {
                var mainViewModel = Ioc.Default.GetRequiredService<MainViewModel>();
                // Prevent Show() from SelectAll-ing the previous query over the folder path.
                mainViewModel.LastQuerySelected = true;
                App.API.ShowMainWindow();
                BringToForeground();
                App.API.ChangeQuery(query);
            }
            finally
            {
                EndHoldWindowOpen();
            }
        });
    }

    private static void BeginHoldWindowOpen()
    {
        if (_holdingWindowOpen)
            return;

        if (Application.Current?.MainWindow is MainWindow mainWindow)
        {
            mainWindow.SuspendHideOnLostFocus();
            _holdingWindowOpen = true;
        }
    }

    private static void EndHoldWindowOpen()
    {
        if (!_holdingWindowOpen)
            return;

        _holdingWindowOpen = false;
        if (Application.Current?.MainWindow is MainWindow mainWindow)
            mainWindow.ResumeHideOnLostFocus();
    }

    private static void BringToForeground()
    {
        if (Application.Current?.MainWindow is MainWindow mainWindow)
            Win32Helper.SetForegroundWindow(mainWindow);
    }
}
