using System;
using System.ComponentModel;
using System.Linq;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.DialogJump;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.Resources.Controls;
using Flow.Launcher.ViewModel;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using DataObject = System.Windows.DataObject;
using Key = System.Windows.Input.Key;
using MouseButtons = System.Windows.Forms.MouseButtons;
using NotifyIcon = System.Windows.Forms.NotifyIcon;

namespace Flow.Launcher
{
    public partial class MainWindow : IDisposable
    {
        #region Public Property

        // Window Event: Close Event
        public bool CanClose { get; set; } = false;

        #endregion

        #region Private Fields

        // Class Name
        private static readonly string ClassName = nameof(MainWindow);

        // Dependency Injection
        private readonly Settings _settings;
        private readonly Theme _theme;

        // Window Notify Icon
        private NotifyIcon _notifyIcon;

        // Window Context Menu
        private readonly ContextMenu _contextMenu = new();
        private readonly MainViewModel _viewModel;

        // Window Event: Key Event
        private bool _isArrowKeyPressed = false;

        // Modal dialogs such as the search-in-folder picker deactivate this window.
        // Hide-on-lost-focus must wait until those dialogs close, or the picker collapses with the owner.
        private int _hideOnLostFocusSuspension;

        // Window Sound Effects
        private MediaPlayer _animationSoundWMP;
        private SoundPlayer _animationSoundWPF;
        private readonly Lock _soundLock = new();

        // Window WndProc
        private HwndSource _hwndSource;
        private int _initialWidth;
        private int _initialHeight;

        // Window Animation
        private const double DefaultRightMargin = 102; //* this value from base.xaml
        private bool _isClockPanelAnimating = false;
        private Storyboard _progressBarStoryboard;

        // IDisposable
        private bool _disposed = false;

        #endregion

        #region Constructor

        public MainWindow()
        {
            _settings = Ioc.Default.GetRequiredService<Settings>();
            _theme = Ioc.Default.GetRequiredService<Theme>();
            _viewModel = Ioc.Default.GetRequiredService<MainViewModel>();
            DataContext = _viewModel;

            Topmost = _settings.ShowAtTopmost;

            InitializeComponent();
            UpdatePosition();

            SyncSoundEffectsState();
            RegisterSoundEffectsEvent();
            DataObject.AddPastingHandler(QueryTextBox, QueryTextBox_OnPaste);
            _viewModel.ActualApplicationThemeChanged += ViewModel_ActualApplicationThemeChanged;
        }

        #endregion

        #region Window Event

#pragma warning disable VSTHRD100 // Avoid async void methods

        private void ViewModel_ActualApplicationThemeChanged(object sender, ActualApplicationThemeChangedEventArgs args)
        {
            // Keep the markdown preview's "Auto" code-highlight theme in step with the app colour scheme.
            PreviewMarkdownScrollViewer.ApplyCodeHighlightTheme(_settings.CodeHighlightTheme, args.IsDark);
            _ = _theme.RefreshFrameAsync();
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            var handle = Win32Helper.GetWindowHandle(this, true);
            _hwndSource = HwndSource.FromHwnd(handle);
            _hwndSource.AddHook(WndProc);
            Win32Helper.HideFromAltTab(this);
            Win32Helper.DisableControlBox(this);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Check first launch
            if (_settings.FirstLaunch)
            {
                // Set First Launch to false
                _settings.FirstLaunch = false;

                // Update release notes version
                _settings.ReleaseNotesVersion = Constant.Version;

                // Set Backdrop Type to Acrylic for Windows 11 when First Launch. Default is None
                if (Win32Helper.IsBackdropSupported()) _settings.BackdropType = BackdropTypes.Acrylic;

                // Save settings
                App.API.SaveAppAllSettings();

                // Show Welcome Window
                var welcomeWindow = new WelcomeWindow();
                welcomeWindow.Show();
            }

            if (Constant.Version != "1.0.0" && _settings.ReleaseNotesVersion != Constant.Version) // Skip release notes notification for developer builds (version 1.0.0)
            {
                // Update release notes version
                _settings.ReleaseNotesVersion = Constant.Version;
                // Show release note popup with button
                App.API.ShowMsgWithButton(
                    Localize.appUpdateTitle(Constant.Version),
                    Localize.appUpdateButtonContent(),
                    () =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var releaseNotesWindow = new ReleaseNotesWindow();
                            releaseNotesWindow.Show();
                        });
                    });
            }

            // Initialize place holder
            SetupPlaceholderText();
            _viewModel.PlaceholderText = _settings.PlaceholderText;

            // Hide window if need
            UpdatePosition();
            if (_settings.HideOnStartup)
            {
                _viewModel.Hide();
                _viewModel.InitializeVisibilityStatus(false);
            }
            else
            {
                _viewModel.Show();
                _viewModel.InitializeVisibilityStatus(true);
                // When HideOnStartup is off and UseAnimation is on,
                // there was a bug where the clock would not appear at all on the initial launch
                // So we need to forcibly trigger animation here to ensure the clock is visible
                if (_settings.UseAnimation)
                {
                    WindowAnimation();
                }
            }

            // Initialize context menu & notify icon
            InitializeContextMenu();
            InitializeNotifyIcon();

            // Initialize color scheme
            if (_settings.ColorScheme == Constant.Light)
            {
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
            }
            else if (_settings.ColorScheme == Constant.Dark)
            {
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
            }

            // Initialize the markdown preview code-highlight theme from settings, resolving "Auto"
            // against the colour scheme just applied above.
            PreviewMarkdownScrollViewer.ApplyCodeHighlightTheme(
                _settings.CodeHighlightTheme,
                ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark);

            // Force update position
            UpdatePosition();

            // Initialize resize mode after refreshing frame
            SetupResizeMode();

            // Reset preview
            // Can't await in sync startup code; fire-and-forget but safely log any failure
            _ = _viewModel.ResetPreviewAsync().ContinueWith(static t =>
                    App.API.LogError(ClassName, $"ResetPreviewAsync failed: {t.Exception}"),
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

            // Since the default main window visibility is visible, so we need set focus during startup
            QueryTextBox.Focus();

            // When the window is shown on startup, focusing QueryTextBox is not enough: the window also
            // has to be activated to actually take OS-level keyboard focus. Otherwise, when Flow Launcher
            // is auto-started with Windows (Startup folder or logon task), the search box looks focused but
            // keystrokes go elsewhere until the user clicks it.
            // This is dispatched at Loaded priority because Activate() throws if the window has not finished
            // being shown yet, and skipped entirely when the window starts hidden for the same reason.
            if (!_settings.HideOnStartup)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!_viewModel.MainWindowVisibilityStatus) return;
                    Activate();
                    QueryTextBox.Focus();
                }), DispatcherPriority.Loaded);
            }

            // Set the initial state of the QueryTextBoxCursorMovedToEnd property
            // Without this part, when shown for the first time, switching the context menu does not move the cursor to the end.
            _viewModel.QueryTextCursorMovedToEnd = false;

            // Register Dialog Jump events
            InitializeDialogJump();

            // View model property changed event
            _viewModel.PropertyChanged += (o, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(MainViewModel.MainWindowVisibilityStatus):
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (_viewModel.MainWindowVisibilityStatus)
                                {
                                    // Play sound effect before activing the window
                                    if (_settings.UseSound && !_viewModel.IsDialogJumpWindowUnderDialog())
                                    {
                                        SoundPlay();
                                    }

                                    // Update position & Activate
                                    UpdatePosition();
                                    Activate();

                                    // Reset preview
                                    // Can't await in Dispatcher.Invoke; fire-and-forget but safely log any failure
                                    _ = _viewModel.ResetPreviewAsync().ContinueWith(static t =>
                                            App.API.LogError(ClassName, $"ResetPreviewAsync failed: {t.Exception}"),
                                        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

                                    // Select last query if need
                                    if (!_viewModel.LastQuerySelected)
                                    {
                                        QueryTextBox.SelectAll();
                                        _viewModel.LastQuerySelected = true;
                                    }

                                    // Focus query box
                                    QueryTextBox.Focus();

                                    // Play window animation
                                    if (_settings.UseAnimation && !_viewModel.IsDialogJumpWindowUnderDialog())
                                    {
                                        WindowAnimation();
                                    }

                                    // Update activate times
                                    _settings.ActivateTimes++;
                                }
                            });
                            break;
                        }
                    case nameof(MainViewModel.QueryTextCursorMovedToEnd):
                        if (_viewModel.QueryTextCursorMovedToEnd)
                        {
                            // QueryTextBox seems to be update with a DispatcherPriority as low as ContextIdle.
                            // To ensure QueryTextBox is up to date with QueryText from the View, we need to Dispatch with such a priority
                            Dispatcher.Invoke(() => QueryTextBox.CaretIndex = QueryTextBox.Text.Length);
                            _viewModel.QueryTextCursorMovedToEnd = false;
                        }
                        break;
                    case nameof(MainViewModel.GameModeStatus):
                        _notifyIcon.Icon = _viewModel.GameModeStatus
                            ? Properties.Resources.gamemode
                            : Properties.Resources.app;
                        break;
                }
            };

            // Settings property changed event
            _settings.PropertyChanged += (o, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(Settings.HideNotifyIcon):
                        _notifyIcon.Visible = !_settings.HideNotifyIcon;
                        break;
                    case nameof(Settings.Language):
                        UpdateNotifyIconText();
                        if (_settings.ShowHomePage && _viewModel.QueryResultsSelected() && string.IsNullOrEmpty(_viewModel.QueryText))
                        {
                            _viewModel.QueryResults();
                        }
                        break;
                    case nameof(Settings.Hotkey):
                        UpdateNotifyIconText();
                        break;
                    case nameof(Settings.WindowLeft):
                        Left = _settings.WindowLeft;
                        break;
                    case nameof(Settings.WindowTop):
                        Top = _settings.WindowTop;
                        break;
                    case nameof(Settings.ShowPlaceholder):
                        SetupPlaceholderText();
                        break;
                    case nameof(Settings.PlaceholderText):
                        _viewModel.PlaceholderText = _settings.PlaceholderText;
                        break;
                    case nameof(Settings.KeepMaxResults):
                        SetupResizeMode();
                        break;
                    case nameof(Settings.SettingWindowFont):
                        InitializeContextMenu();
                        break;
                    case nameof(Settings.ShowHomePage):
                    case nameof(Settings.ShowHistoryResultsForHomePage):
                    case nameof(Settings.HistoryStyle):
                    case nameof(Settings.HistorySortOrderForHomePage):
                        if (_viewModel.QueryResultsSelected() && string.IsNullOrEmpty(_viewModel.QueryText))
                        {
                            _viewModel.QueryResults();
                        }
                        break;
                    case nameof(Settings.ShowAtTopmost):
                        Topmost = _settings.ShowAtTopmost;
                        break;
                    case nameof(Settings.UseSound):
                        SyncSoundEffectsState();
                        break;
                }
            };

            // QueryTextBox.Text change detection (modified to only work when character count is 1 or higher)
            QueryTextBox.TextChanged += (s, e) => UpdateClockPanelVisibility();

            // Detecting ResultContextMenu.Visibility changes
            DependencyPropertyDescriptor
                .FromProperty(VisibilityProperty, typeof(ResultListBox))
                .AddValueChanged(ResultContextMenu, (s, e) => UpdateClockPanelVisibility());

            // Detect History.Visibility changes
            DependencyPropertyDescriptor
                .FromProperty(VisibilityProperty, typeof(ResultListBox))
                .AddValueChanged(History, (s, e) => UpdateClockPanelVisibility());

            // Initialize query state
            if ((_settings.ShowHomePage || _settings.ShowHistoryResultsForHomePage) && string.IsNullOrEmpty(_viewModel.QueryText))
            {
                _viewModel.QueryResults();
            }
        }

        private void ProgressBar_Loaded(object sender, RoutedEventArgs e)
        {
            InitProgressbarAnimation();
        }

        private async void OnClosing(object sender, CancelEventArgs e)
        {
            if (!CanClose)
            {
                App.API.LogInfo(ClassName, "Main window closing; shutting down the process");
                CanClose = true;
                _notifyIcon.Visible = false;
                App.API.SaveAppAllSettings();
                e.Cancel = true;
                await PluginManager.DisposePluginsAsync();
                Notification.Uninstall();
                // After plugins are all disposed, we shutdown application to close app
                // We use this instead of Close() to avoid InvalidOperationException when calling Close() in OnClosing event
                Application.Current.Shutdown();
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            try
            {
                _hwndSource.RemoveHook(WndProc);
            }
            catch (Exception)
            {
                // Ignored
            }

            _hwndSource = null;
        }

        private void OnLocationChanged(object sender, EventArgs e)
        {
            if (_viewModel.IsDialogJumpWindowUnderDialog())
            {
                return;
            }

            if (IsLoaded)
            {
                _settings.WindowLeft = Left;
                _settings.WindowTop = Top;
            }
        }

        internal void SuspendHideOnLostFocus() => _hideOnLostFocusSuspension++;

        internal void ResumeHideOnLostFocus()
        {
            // The deactivation handler may still be waiting out its animation delay inside the
            // dialog message loop. Release only after that continuation has had a chance to see
            // the suspension and skip the hide.
            Dispatcher.BeginInvoke(ReleaseHideOnLostFocusSuspension, DispatcherPriority.ApplicationIdle);
        }

        private void ReleaseHideOnLostFocusSuspension()
        {
            if (_hideOnLostFocusSuspension > 0)
            {
                _hideOnLostFocusSuspension--;
            }
        }

        private async void OnDeactivated(object sender, EventArgs e)
        {
            if (_viewModel.IsDialogJumpWindowUnderDialog() || _hideOnLostFocusSuspension > 0)
            {
                return;
            }

            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;

            _viewModel.ClockPanelOpacity = 0.0;
            _viewModel.SearchIconOpacity = 0.0;

            // This condition stops extra hide call when animator is on,
            // which causes the toggling to occasional hide instead of show.
            if (_viewModel.MainWindowVisibilityStatus)
            {
                // Need time to initialize the main query window animation.
                // This also stops the mainwindow from flickering occasionally after Settings window is opened
                // and always after Settings window is closed.
                if (_settings.UseAnimation)
                {
                    await Task.Delay(100);
                }

                if (_hideOnLostFocusSuspension > 0)
                {
                    return;
                }

                if (_settings.HideWhenDeactivated && !_viewModel.ExternalPreviewVisible)
                {
                    _viewModel.Hide();
                }
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // When a code-block in the markdown preview is focused
            // Let it capture input of text navigation keys (arrows, page, home/end) instead
            // Non-navigation keys pass through normally.
            if (PreviewMarkdownScrollViewer.IsCodeBlockFocused(e.OriginalSource)
                && PreviewMarkdownScrollViewer.IsCodeBlockNavigationKey(e.Key))
            {
                return;
            }

            var specialKeyState = GlobalHotkey.CheckModifiers();
            switch (e.Key)
            {
                case Key.Down:
                    _isArrowKeyPressed = true;
                    _viewModel.SelectNextItemCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Up:
                    _isArrowKeyPressed = true;
                    _viewModel.SelectPrevItemCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.PageDown:
                    _viewModel.SelectNextPageCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.PageUp:
                    _viewModel.SelectPrevPageCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Right:
                    if (_viewModel.QueryResultsSelected()
                        && QueryTextBox.CaretIndex == QueryTextBox.Text.Length)            
                    {
                        _viewModel.LoadContextMenuCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.Left:
                    if (!_viewModel.QueryResultsSelected() && QueryTextBox.CaretIndex == 0)
                    {
                        _viewModel.EscCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.Back:
                    if (specialKeyState.CtrlPressed)
                    {
                        if (_viewModel.QueryResultsSelected()
                            && QueryTextBox.Text.Length > 0
                            && QueryTextBox.CaretIndex == QueryTextBox.Text.Length)
                        {
                            var queryWithoutActionKeyword =
                                QueryBuilder.Build(QueryTextBox.Text, QueryTextBox.Text.Trim(), PluginManager.GetNonGlobalPlugins())?.Search;

                            if (FilesFolders.IsLocationPathString(queryWithoutActionKeyword))
                            {
                                _viewModel.BackspaceCommand.Execute(null);
                                e.Handled = true;
                            }
                        }
                    }
                    break;
                case Key.Delete:
                    if (_viewModel.CanRemoveSelectedHistoryItem())
                    {
                        _viewModel.RemoveSelectedHistoryItemCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                default:
                    break;
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up || e.Key == Key.Down)
            {
                _isArrowKeyPressed = false;
            }
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isArrowKeyPressed)
            {
                e.Handled = true; // Ignore Mouse Hover when press Arrowkeys
            }
        }

#pragma warning restore VSTHRD100 // Avoid async void methods

        #endregion

        #region Window Boarder Event

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            // When the window is maximized via Snap,
            // dragging attempts will first switch the window from Maximized to Normal state,
            // and adjust the drag position accordingly.
            if (e.ChangedButton == MouseButton.Left)
            {
                try
                {
                    if (WindowState == WindowState.Maximized)
                    {
                        // Calculate ratio based on maximized window dimensions
                        double maxWidth = ActualWidth;
                        double maxHeight = ActualHeight;
                        var mousePos = e.GetPosition(this);
                        double xRatio = mousePos.X / maxWidth;
                        double yRatio = mousePos.Y / maxHeight;

                        // Current monitor information
                        var screen = MonitorInfo.GetNearestDisplayMonitor(new WindowInteropHelper(this).Handle);
                        var workingArea = screen.WorkingArea;
                        var screenLeftTop = Win32Helper.TransformPixelsToDIP(this, workingArea.X, workingArea.Y);

                        // Switch to Normal state
                        WindowState = WindowState.Normal;

                        Application.Current?.Dispatcher.Invoke(new Action(() =>
                        {
                            double normalWidth = Width;
                            double normalHeight = Height;

                            // Apply ratio based on the difference between maximized and normal window sizes
                            Left = screenLeftTop.X + (maxWidth - normalWidth) * xRatio;
                            Top = screenLeftTop.Y + (maxHeight - normalHeight) * yRatio;

                            if (Mouse.LeftButton == MouseButtonState.Pressed)
                            {
                                DragMove();
                            }
                        }), DispatcherPriority.ApplicationIdle);
                    }
                    else
                    {
                        DragMove();
                    }
                }
                catch (InvalidOperationException)
                {
                    // Ignored - can occur if drag operation is already in progress
                }
            }
        }

        #endregion

        #region Window Context Menu Event

#pragma warning disable VSTHRD100 // Avoid async void methods

        private async void OnContextMenusForSettingsClick(object sender, RoutedEventArgs e)
        {
            _viewModel.Hide();

            if (_settings.UseAnimation)
                await Task.Delay(100);

            App.API.OpenSettingDialog();
        }

#pragma warning restore VSTHRD100 // Avoid async void methods

        #endregion

        #region Resize Mode

        private void SetupResizeMode()
        {
            ResizeMode = _settings.KeepMaxResults ? ResizeMode.NoResize : ResizeMode.CanResize;
            if (WindowChrome.GetWindowChrome(this) is WindowChrome windowChrome)
            {
                _theme.SetResizeBorderThickness(windowChrome, _settings.KeepMaxResults);
            }
        }

        #endregion

        #region Search Delay

        private void QueryTextBox_TextChanged1(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            _viewModel.QueryText = textBox.Text;
            _viewModel.Query(_settings.SearchQueryResultsWithDelay);
        }

        #endregion

        #region Dialog Jump

        private void InitializeDialogJump()
        {
            DialogJump.ShowDialogJumpWindowAsync = _viewModel.SetupDialogJumpAsync;
            DialogJump.UpdateDialogJumpWindow = InitializeDialogJumpPosition;
            DialogJump.ResetDialogJumpWindow = _viewModel.ResetDialogJump;
            DialogJump.HideDialogJumpWindow = _viewModel.HideDialogJump;
        }

        private void InitializeDialogJumpPosition()
        {
            if (_viewModel.DialogWindowHandle == nint.Zero || !_viewModel.MainWindowVisibilityStatus) return;
            if (!_viewModel.IsDialogJumpWindowUnderDialog()) return;

            // Get dialog window rect
            var result = Win32Helper.GetWindowRect(_viewModel.DialogWindowHandle, out var window);
            if (!result) return;

            // Move window below the bottom of the dialog and keep it center
            Top = VerticalBottom(window);
            Left = HorizonCenter(window);
        }

        private double HorizonCenter(Rect window)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, window.X, 0);
            var dip2 = Win32Helper.TransformPixelsToDIP(this, window.Width, 0);
            var left = (dip2.X - ActualWidth) / 2 + dip1.X;
            return left;
        }

        private double VerticalBottom(Rect window)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, 0, window.Bottom);
            return dip1.Y;
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _hwndSource?.Dispose();
                    _notifyIcon?.Dispose();
                    UnregisterSoundEffectsEvent();
                    DisposeSoundEffects();
                    _viewModel.ActualApplicationThemeChanged -= ViewModel_ActualApplicationThemeChanged;
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
