using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher
{
    public partial class MainWindow
    {
        #region Window Position

        public void UpdatePosition()
        {
            // Initialize call twice to workaround multi-display alignment issue- https://github.com/Flow-Launcher/Flow.Launcher/issues/2910
            if (_viewModel.IsDialogJumpWindowUnderDialog())
            {
                InitializeDialogJumpPosition();
                InitializeDialogJumpPosition();
            }
            else
            {
                InitializePosition();
                InitializePosition();
            }
        }

        private async Task PositionResetAsync()
        {
            _viewModel.Show();
            await Task.Delay(300); // If don't give a time, Positioning will be weird.
            var screen = SelectedScreen();
            Left = HorizonCenter(screen);
            Top = VerticalCenter(screen);
        }

        private void InitializePosition()
        {
            // Initialize call twice to workaround multi-display alignment issue- https://github.com/Flow-Launcher/Flow.Launcher/issues/2910
            InitializePositionInner();
            InitializePositionInner();
            return;

            void InitializePositionInner()
            {
                if (_settings.SearchWindowScreen == SearchWindowScreens.RememberLastLaunchLocation)
                {
                    var previousScreenWidth = _settings.PreviousScreenWidth;
                    var previousScreenHeight = _settings.PreviousScreenHeight;
                    GetDpi(out var previousDpiX, out var previousDpiY);

                    _settings.PreviousScreenWidth = SystemParameters.VirtualScreenWidth;
                    _settings.PreviousScreenHeight = SystemParameters.VirtualScreenHeight;
                    GetDpi(out var currentDpiX, out var currentDpiY);

                    if (previousScreenWidth != 0 && previousScreenHeight != 0 &&
                        previousDpiX != 0 && previousDpiY != 0 &&
                        (previousScreenWidth != SystemParameters.VirtualScreenWidth ||
                         previousScreenHeight != SystemParameters.VirtualScreenHeight ||
                         previousDpiX != currentDpiX || previousDpiY != currentDpiY))
                    {
                        AdjustPositionForResolutionChange();
                        return;
                    }

                    Left = _settings.WindowLeft;
                    Top = _settings.WindowTop;
                }
                else
                {
                    var screen = SelectedScreen();
                    switch (_settings.SearchWindowAlign)
                    {
                        case SearchWindowAligns.Center:
                            Left = HorizonCenter(screen);
                            Top = VerticalCenter(screen);
                            break;
                        case SearchWindowAligns.CenterTop:
                            Left = HorizonCenter(screen);
                            Top = VerticalTop(screen);
                            break;
                        case SearchWindowAligns.LeftTop:
                            Left = HorizonLeft(screen);
                            Top = VerticalTop(screen);
                            break;
                        case SearchWindowAligns.RightTop:
                            Left = HorizonRight(screen);
                            Top = VerticalTop(screen);
                            break;
                        case SearchWindowAligns.Custom:
                            var customLeft = Win32Helper.TransformPixelsToDIP(this,
                                screen.WorkingArea.X + _settings.CustomWindowLeft, 0);
                            var customTop = Win32Helper.TransformPixelsToDIP(this, 0,
                                screen.WorkingArea.Y + _settings.CustomWindowTop);
                            Left = customLeft.X;
                            Top = customTop.Y;
                            break;
                    }
                }
            }
        }

        private void AdjustPositionForResolutionChange()
        {
            var screenWidth = SystemParameters.VirtualScreenWidth;
            var screenHeight = SystemParameters.VirtualScreenHeight;
            GetDpi(out var currentDpiX, out var currentDpiY);

            var previousLeft = _settings.WindowLeft;
            var previousTop = _settings.WindowTop;
            GetDpi(out var previousDpiX, out var previousDpiY);

            var widthRatio = screenWidth / _settings.PreviousScreenWidth;
            var heightRatio = screenHeight / _settings.PreviousScreenHeight;
            var dpiXRatio = currentDpiX / previousDpiX;
            var dpiYRatio = currentDpiY / previousDpiY;

            var newLeft = previousLeft * widthRatio * dpiXRatio;
            var newTop = previousTop * heightRatio * dpiYRatio;

            var screenLeft = SystemParameters.VirtualScreenLeft;
            var screenTop = SystemParameters.VirtualScreenTop;

            var maxX = screenLeft + screenWidth - ActualWidth;
            var maxY = screenTop + screenHeight - ActualHeight;

            Left = Math.Max(screenLeft, Math.Min(newLeft, maxX));
            Top = Math.Max(screenTop, Math.Min(newTop, maxY));
        }

        private void GetDpi(out double dpiX, out double dpiY)
        {
            var source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                var matrix = source.CompositionTarget.TransformToDevice;
                dpiX = 96 * matrix.M11;
                dpiY = 96 * matrix.M22;
            }
            else
            {
                dpiX = 96;
                dpiY = 96;
            }
        }

        private MonitorInfo SelectedScreen()
        {
            MonitorInfo screen;
            switch (_settings.SearchWindowScreen)
            {
                case SearchWindowScreens.Cursor:
                    screen = MonitorInfo.GetCursorDisplayMonitor();
                    break;
                case SearchWindowScreens.Focus:
                    screen = MonitorInfo.GetNearestDisplayMonitor(Win32Helper.GetForegroundWindow());
                    break;
                case SearchWindowScreens.Primary:
                    screen = MonitorInfo.GetPrimaryDisplayMonitor();
                    break;
                case SearchWindowScreens.Custom:
                    var allScreens = MonitorInfo.GetDisplayMonitors();
                    if (_settings.CustomScreenNumber <= allScreens.Count)
                        screen = allScreens[_settings.CustomScreenNumber - 1];
                    else
                        screen = allScreens[0];
                    break;
                default:
                    screen = MonitorInfo.GetDisplayMonitors()[0];
                    break;
            }

            return screen ?? MonitorInfo.GetDisplayMonitors()[0];
        }

        private double HorizonCenter(MonitorInfo screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip2.X - ActualWidth) / 2 + dip1.X;
            return left;
        }

        private double VerticalCenter(MonitorInfo screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
            var dip2 = Win32Helper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
            var top = (dip2.Y - QueryTextBox.ActualHeight) / 4 + dip1.Y;
            return top;
        }

        private double HorizonRight(MonitorInfo screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip1.X + dip2.X - ActualWidth) - 10;
            return left;
        }

        private double HorizonLeft(MonitorInfo screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var left = dip1.X + 10;
            return left;
        }

        private double VerticalTop(MonitorInfo screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
            var top = dip1.Y + 10;
            return top;
        }

        #endregion
    }
}
