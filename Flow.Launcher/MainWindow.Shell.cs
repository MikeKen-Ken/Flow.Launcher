using System;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Flow.Launcher.Infrastructure;
using iNKORE.UI.WPF.Modern.Controls;
using MouseButtons = System.Windows.Forms.MouseButtons;
using NotifyIcon = System.Windows.Forms.NotifyIcon;

namespace Flow.Launcher
{
    public partial class MainWindow
    {
        #region Window WndProc

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) 
        {
            switch (msg)
            {
                case Win32Helper.WM_ENTERSIZEMOVE:
                    // Do do handle size move event for dialog jump window
                    if (_viewModel.IsDialogJumpWindowUnderDialog())
                    {
                        return IntPtr.Zero;
                    }

                    _initialWidth = (int)Width;
                    _initialHeight = (int)Height;
                    handled = true;
                    break;
                case Win32Helper.WM_EXITSIZEMOVE:
                    // Do do handle size move event for Dialog Jump window
                    if (_viewModel.IsDialogJumpWindowUnderDialog())
                    {
                        return IntPtr.Zero;
                    }

                    //Prevent updating the number of results when the window height is below the height of a single result item.
                    //This situation occurs not only when the user manually resizes the window, but also when the window is released from a side snap, as the OS automatically adjusts the window height.
                    //(Without this check, releasing from a snap can cause the window height to hit the minimum, resulting in only 2 results being shown.)
                    if (_initialHeight != (int)Height && Height > (_settings.WindowHeightSize + _settings.ItemHeightSize))
                    {
                        if (!_settings.KeepMaxResults)
                        {
                            // Get shadow margin
                            var shadowMargin = 0;
                            var (_, useDropShadowEffect) = _theme.GetActualValue();
                            if (useDropShadowEffect)
                            {
                                shadowMargin = 32;
                            }

                            // Calculate max results to show
                            var itemCount = (Height - (_settings.WindowHeightSize + 14) - shadowMargin) / _settings.ItemHeightSize;
                            if (itemCount < 2)
                            {
                                _settings.MaxResultsToShow = 2;
                            }
                            else
                            {
                                _settings.MaxResultsToShow = Convert.ToInt32(Math.Truncate(itemCount));
                            }
                        }

                        SizeToContent = SizeToContent.Height;
                    }
                    else
                    {
                        // Update height when exiting maximized snap state.
                        SizeToContent = SizeToContent.Height;
                    }

                    if (_initialWidth != (int)Width)
                    {
                        if (!_settings.KeepMaxResults)
                        {
                            // Update width
                            _viewModel.MainWindowWidth = Width;
                        }

                        SizeToContent = SizeToContent.Height;
                    }

                    handled = true;
                    break;
                case Win32Helper.WM_NCLBUTTONDBLCLK: // Block the double click in frame
                    SizeToContent = SizeToContent.Height;
                    handled = true;
                    break;
                case Win32Helper.WM_SYSCOMMAND: // Block Maximize/Minimize by Win+Up and Win+Down Arrow
                    var command = wParam.ToInt32() & 0xFFF0;
                    if (command == Win32Helper.SC_MAXIMIZE || command == Win32Helper.SC_MINIMIZE)
                    {
                        SizeToContent = SizeToContent.Height;
                        handled = true;
                    }
                    break;
            }

            return IntPtr.Zero;
        }

        #endregion

        #region Window Sound Effects

        private void InitSoundEffects()
        {
            lock (_soundLock)
            {
                if (_settings.WMPInstalled)
                {
                    _animationSoundWMP?.Close();
                    _animationSoundWMP = new MediaPlayer();
                    _animationSoundWMP.Open(new Uri(AppContext.BaseDirectory + "Resources\\open.wav"));
                }
                else
                {
                    _animationSoundWPF?.Dispose();
                    _animationSoundWPF = new SoundPlayer(AppContext.BaseDirectory + "Resources\\open.wav");
                    _animationSoundWPF.Load();
                }
            }
        }

        private void SoundPlay()
        {
            lock (_soundLock)
            {
                if (_settings.WMPInstalled)
                {
                    if (_animationSoundWMP == null)
                    {
                        return;
                    }

                    _animationSoundWMP.Position = TimeSpan.Zero;
                    _animationSoundWMP.Volume = _settings.SoundVolume / 100.0;
                    _animationSoundWMP.Play();
                }
                else
                {
                    if (_animationSoundWPF == null)
                    {
                        return;
                    }

                    _animationSoundWPF.Play();
                }
            }
        }

        private bool IsSoundEffectsInitialized()
        {
            lock (_soundLock)
            {
                return _animationSoundWMP != null || _animationSoundWPF != null;
            }
        }

        private void DisposeSoundEffects()
        {
            lock (_soundLock)
            {
                _animationSoundWMP?.Stop();
                _animationSoundWMP?.Close();
                _animationSoundWMP = null;

                _animationSoundWPF?.Stop();
                _animationSoundWPF?.Dispose();
                _animationSoundWPF = null;
            }
        }

        private void SyncSoundEffectsState(bool forceReinitializeWhenEnabled = false)
        {
            if (!_settings.UseSound)
            {
                if (IsSoundEffectsInitialized())
                {
                    DisposeSoundEffects();
                }

                return;
            }

            if (forceReinitializeWhenEnabled || !IsSoundEffectsInitialized())
            {
                InitSoundEffects();
            }
        }

        private void RegisterSoundEffectsEvent()
        {
            // Fix for sound not playing after sleep / hibernate for both modern standby and legacy standby
            // https://stackoverflow.com/questions/64805186/mediaplayer-doesnt-play-after-computer-sleeps
            try
            {
                Win32Helper.RegisterSleepModeListener(() =>
                {
                    if (Application.Current == null)
                    {
                        return;
                    }

                    // We must run SyncSoundEffectsState on UI thread because MediaPlayer is a DispatcherObject
                    if (!Application.Current.Dispatcher.CheckAccess())
                    {
                        Application.Current.Dispatcher.Invoke(() => SyncSoundEffectsState(forceReinitializeWhenEnabled: true));
                        return;
                    }

                    SyncSoundEffectsState(forceReinitializeWhenEnabled: true);
                });
            }
            catch (Exception e)
            {
                App.API.LogException(ClassName, "Failed to register sound effect event", e);
            }
        }

        private static void UnregisterSoundEffectsEvent()
        {
            try
            {
                Win32Helper.UnregisterSleepModeListener();
            }
            catch (Exception e)
            {
                App.API.LogException(ClassName, "Failed to unregister sound effect event", e);
            }
        }

        #endregion

        #region Window Notify Icon

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Text = Constant.FlowLauncherFullName,
                Icon = Constant.Version == "1.0.0" ? Properties.Resources.dev : Properties.Resources.app,
                Visible = !_settings.HideNotifyIcon
            };

            _notifyIcon.MouseClick += (o, e) =>
            {
                switch (e.Button)
                {
                    case MouseButtons.Left:
                        _viewModel.ToggleFlowLauncher();
                        break;
                    case MouseButtons.Right:

                        _contextMenu.IsOpen = true;
                        // Get context menu handle and bring it to the foreground
                        if (PresentationSource.FromVisual(_contextMenu) is HwndSource hwndSource)
                        {
                            Win32Helper.SetForegroundWindow(hwndSource.Handle);
                        }

                        _contextMenu.Focus();
                        break;
                }
            };
        }

        private void UpdateNotifyIconText()
        {
            var menu = _contextMenu;
            ((MenuItem)menu.Items[0]).Header = Localize.iconTrayOpen() +
                                               " (" + _settings.Hotkey + ")";
            ((MenuItem)menu.Items[1]).Header = Localize.GameMode();
            ((MenuItem)menu.Items[2]).Header = Localize.PositionReset();
            ((MenuItem)menu.Items[3]).Header = Localize.iconTraySettings();
            ((MenuItem)menu.Items[4]).Header = Localize.iconTrayExit();
        }

        private void InitializeContextMenu()
        {
            var menu = _contextMenu;
            menu.Items.Clear();
            var openIcon = new FontIcon { Glyph = "\ue71e" };
            var open = new MenuItem
            {
                Header = Localize.iconTrayOpen() + " (" + _settings.Hotkey + ")",
                Icon = openIcon
            };
            var gamemodeIcon = new FontIcon { Glyph = "\ue7fc" };
            var gamemode = new MenuItem
            {
                Header = Localize.GameMode(),
                Icon = gamemodeIcon
            };
            var positionresetIcon = new FontIcon { Glyph = "\ue73f" };
            var positionreset = new MenuItem
            {
                Header = Localize.PositionReset(),
                Icon = positionresetIcon
            };
            var settingsIcon = new FontIcon { Glyph = "\ue713" };
            var settings = new MenuItem
            {
                Header = Localize.iconTraySettings(),
                Icon = settingsIcon
            };
            var exitIcon = new FontIcon { Glyph = "\ue7e8" };
            var exit = new MenuItem
            {
                Header = Localize.iconTrayExit(),
                Icon = exitIcon
            };

            open.Click += (o, e) => _viewModel.ToggleFlowLauncher();
            gamemode.Click += (o, e) => _viewModel.ToggleGameMode();
            positionreset.Click += (o, e) => _ = PositionResetAsync();
            settings.Click += (o, e) => App.API.OpenSettingDialog();
            exit.Click += (o, e) => Close();

            gamemode.ToolTip = Localize.GameModeToolTip();
            positionreset.ToolTip = Localize.PositionResetToolTip();

            _contextMenu.Items.Add(open);
            _contextMenu.Items.Add(gamemode);
            _contextMenu.Items.Add(positionreset);
            _contextMenu.Items.Add(settings);
            _contextMenu.Items.Add(exit);
        }

        #endregion
    }
}
