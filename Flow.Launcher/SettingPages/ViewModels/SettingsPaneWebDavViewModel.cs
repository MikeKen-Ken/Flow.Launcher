using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.WebDavSync;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneWebDavViewModel : BaseModel
{
    private readonly WebDavSyncService _syncService;

    public Settings Settings { get; }

    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRunActions));
            }
        }
    }

    public bool CanRunActions => !IsBusy;

    public string LastSyncDisplay =>
        Settings.WebDavSync.LastSuccessfulSyncUtc is { } timestamp
            ? Localize.webDavSyncLastSyncAt(timestamp.ToLocalTime().ToString("G"))
            : Localize.webDavSyncLastSyncNever();

    public SettingsPaneWebDavViewModel(Settings settings) : this(settings, new WebDavSyncService())
    {
    }

    public SettingsPaneWebDavViewModel(Settings settings, WebDavSyncService syncService)
    {
        Settings = settings;
        _syncService = syncService;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _syncService.TestConnectionAsync(Settings.WebDavSync).ConfigureAwait(true);
            App.API.ShowMsgBox(Localize.webDavSyncTestSuccess(), Localize.webDavSync());
        }
        catch (Exception e)
        {
            App.API.ShowMsgBox(Localize.webDavSyncTestFailed(e.Message), Localize.webDavSync());
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task UploadAsync() => RunOperationAsync(WebDavSyncOperation.Upload);

    [RelayCommand]
    private Task DownloadAsync() => RunOperationAsync(WebDavSyncOperation.Download);

    [RelayCommand]
    private Task SyncAsync() => RunOperationAsync(WebDavSyncOperation.Sync);

    private async Task RunOperationAsync(WebDavSyncOperation operation)
    {
        if (IsBusy)
        {
            return;
        }

        var owner = Application.Current.Windows.OfType<SettingWindow>().FirstOrDefault()
                    ?? Application.Current.MainWindow;
        if (!WebDavSyncConfirmWindow.Confirm(owner, operation))
        {
            return;
        }

        Settings.Save();
        App.API.SavePluginSettings();

        IsBusy = true;
        WebDavSyncResult result = null;
        using var cancellation = new CancellationTokenSource();

        try
        {
            await ProgressBoxEx.ShowAsync(
                Localize.webDavSyncProgress(),
                async reportProgress =>
                {
                    result = await _syncService.ExecuteAsync(
                        operation,
                        Settings.WebDavSync,
                        reportProgress,
                        cancellation.Token).ConfigureAwait(false);
                },
                cancellation.Cancel).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            Settings.Save();
            OnPropertyChanged(nameof(LastSyncDisplay));
        }

        if (result == null)
        {
            return;
        }

        if (!result.Success)
        {
            App.API.ShowMsgBox(Localize.webDavSyncFailed(result.ErrorMessage), Localize.webDavSync());
            return;
        }

        App.API.ShowMsgBox(GetSuccessMessage(result), Localize.webDavSync());

        if (result.RequiresRestart)
        {
            App.API.RestartApp();
        }
    }

    private static string GetSuccessMessage(WebDavSyncResult result) => result.ActionTaken switch
    {
        WebDavSyncActionTaken.Uploaded => Localize.webDavSyncSuccessUpload(),
        WebDavSyncActionTaken.Downloaded => Localize.webDavSyncSuccessDownload(),
        WebDavSyncActionTaken.AlreadyInSync => Localize.webDavSyncAlreadyInSync(),
        _ => Localize.webDavSyncSuccessSync()
    };
}
