using System.Windows;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPaneWebDav
{
    private SettingsPaneWebDavViewModel _viewModel = null!;
    private readonly SettingWindowViewModel _settingViewModel = Ioc.Default.GetRequiredService<SettingWindowViewModel>();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _settingViewModel.PageType = typeof(SettingsPaneWebDav);

        if (_viewModel == null)
        {
            _viewModel = Ioc.Default.GetRequiredService<SettingsPaneWebDavViewModel>();
            DataContext = _viewModel;
        }
        if (!IsInitialized)
        {
            InitializeComponent();
            WebDavPasswordBox.Password = _viewModel.Settings.WebDavSync.Password;
        }
        base.OnNavigatedTo(e);
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsPaneWebDavViewModel viewModel)
        {
            viewModel.Settings.WebDavSync.Password = WebDavPasswordBox.Password;
        }
    }
}
