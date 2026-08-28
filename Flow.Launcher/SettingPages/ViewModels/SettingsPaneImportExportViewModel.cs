using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.ImportExport;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Microsoft.Win32;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneImportExportViewModel : BaseModel
{
    private readonly ImportExportService _importExportService;

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

    private bool _includeSettings = true;

    public bool IncludeSettings
    {
        get => _includeSettings;
        set
        {
            if (_includeSettings != value)
            {
                _includeSettings = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _includePlugins = true;

    public bool IncludePlugins
    {
        get => _includePlugins;
        set
        {
            if (_includePlugins != value)
            {
                _includePlugins = value;
                OnPropertyChanged();
            }
        }
    }

    public SettingsPaneImportExportViewModel(Settings settings)
        : this(settings, new ImportExportService())
    {
    }

    public SettingsPaneImportExportViewModel(Settings settings, ImportExportService importExportService)
    {
        Settings = settings;
        _importExportService = importExportService;
    }

    [RelayCommand]
    private Task ExportToFolderAsync() => RunExportAsync(
        () => PickFolder(Localize.importExportSelectExportFolder()),
        (path, reportProgress, token) => _importExportService.ExportToFolder(
            CreateBackupFolder(path), IncludeSettings, IncludePlugins, reportProgress, token));

    [RelayCommand]
    private Task ExportToZipAsync() => RunExportAsync(
        () => PickSaveZip(Localize.importExportSelectExportZip()),
        (path, reportProgress, token) => _importExportService.ExportToZip(
            path, IncludeSettings, IncludePlugins, reportProgress, token));

    [RelayCommand]
    private Task ImportFromFolderAsync() => RunImportAsync(
        () => PickFolder(Localize.importExportSelectImportFolder()),
        (path, reportProgress, token) => _importExportService.ImportFromFolder(
            path, IncludeSettings, IncludePlugins, reportProgress, token));

    [RelayCommand]
    private Task ImportFromZipAsync() => RunImportAsync(
        () => PickOpenZip(Localize.importExportSelectImportZip()),
        (path, reportProgress, token) => _importExportService.ImportFromZip(
            path, IncludeSettings, IncludePlugins, reportProgress, token));

    private async Task RunExportAsync(
        Func<string> pickPath,
        Func<string, Action<double>, CancellationToken, ImportExportResult> execute)
    {
        if (IsBusy)
        {
            return;
        }

        var path = pickPath();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        await ExecuteAsync(execute, path, restartOnSuccess: false, Localize.importExportSuccessExport()).ConfigureAwait(true);
    }

    private async Task RunImportAsync(
        Func<string> pickPath,
        Func<string, Action<double>, CancellationToken, ImportExportResult> execute)
    {
        if (IsBusy)
        {
            return;
        }

        var path = pickPath();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var confirmed = App.API.ShowMsgBox(
            Localize.importExportConfirmImport(Environment.NewLine),
            Localize.importExport(),
            MessageBoxButton.YesNo) == MessageBoxResult.Yes;
        if (!confirmed)
        {
            return;
        }

        await ExecuteAsync(execute, path, restartOnSuccess: true, Localize.importExportSuccessImport()).ConfigureAwait(true);
    }

    private async Task ExecuteAsync(
        Func<string, Action<double>, CancellationToken, ImportExportResult> execute,
        string path,
        bool restartOnSuccess,
        string successMessage)
    {
        Settings.Save();
        App.API.SavePluginSettings();

        IsBusy = true;
        ImportExportResult result = null;
        using var cancellation = new CancellationTokenSource();

        try
        {
            await ProgressBoxEx.ShowAsync(
                Localize.importExportProgress(),
                async reportProgress =>
                {
                    result = await Task.Run(
                        () => execute(path, reportProgress, cancellation.Token),
                        cancellation.Token).ConfigureAwait(false);
                },
                cancellation.Cancel).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }

        if (result == null)
        {
            return;
        }

        if (!result.Success)
        {
            App.API.ShowMsgBox(Localize.importExportFailed(result.ErrorMessage), Localize.importExport());
            return;
        }

        App.API.ShowMsgBox(successMessage, Localize.importExport());

        if (restartOnSuccess && result.RequiresRestart)
        {
            App.API.RestartApp();
        }
    }

    private static string CreateBackupFolder(string parentDirectory)
    {
        var folderName = $"FlowLauncher-Backup-{DateTime.Now:yyyyMMdd-HHmmss}";
        return System.IO.Path.Combine(parentDirectory, folderName);
    }

    private static string PickFolder(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : string.Empty;
    }

    private static string PickSaveZip(string title)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = Localize.importExportZipFilter(),
            DefaultExt = ".zip",
            AddExtension = true,
            FileName = $"FlowLauncher-Backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : string.Empty;
    }

    private static string PickOpenZip(string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = Localize.importExportZipFilter(),
            DefaultExt = ".zip",
            Multiselect = false,
            CheckFileExists = true,
            CheckPathExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : string.Empty;
    }
}
