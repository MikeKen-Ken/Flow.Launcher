using System.Threading;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
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

    private static void Apply(string folderPath)
    {
        var query = FolderSearchCommand.BuildQuery(folderPath);
        if (string.IsNullOrEmpty(query))
            return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
            return;

        dispatcher.Invoke(() =>
        {
            var mainViewModel = Ioc.Default.GetRequiredService<MainViewModel>();
            // Prevent Show() from SelectAll-ing the previous query over the folder path.
            mainViewModel.LastQuerySelected = true;
            App.API.ShowMainWindow();
            App.API.ChangeQuery(query);
        });
    }
}
