using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher
{
    public partial class MainWindow
    {
        #region Window Animation

        private void InitProgressbarAnimation()
        {
            _progressBarStoryboard = new Storyboard();

            var animationDuration = new Duration(TimeSpan.FromMilliseconds(1600));
            var progressBarLength = ProgressBar.X2 - ProgressBar.X1;

            var lineEndAnimation = new DoubleAnimation
            {
                From = ProgressBar.X2,
                To = ProgressBar.ActualWidth + progressBarLength,
                Duration = animationDuration
            };
            var lineStartAnimation = new DoubleAnimation
            {
                From = ProgressBar.X1,
                To = ProgressBar.ActualWidth,
                Duration = animationDuration
            };
            
            Storyboard.SetTarget(lineEndAnimation, ProgressBar);
            Storyboard.SetTargetProperty(lineEndAnimation, new PropertyPath("(Line.X2)"));
            
            Storyboard.SetTarget(lineStartAnimation, ProgressBar);
            Storyboard.SetTargetProperty(lineStartAnimation, new PropertyPath("(Line.X1)"));
            
            _progressBarStoryboard.Children.Add(lineEndAnimation);
            _progressBarStoryboard.Children.Add(lineStartAnimation);
            _progressBarStoryboard.RepeatBehavior = RepeatBehavior.Forever;

            lineEndAnimation.Freeze();
            lineStartAnimation.Freeze();

            ProgressBar.IsVisibleChanged -= ProgressBar_IsVisibleChanged;
            ProgressBar.IsVisibleChanged += ProgressBar_IsVisibleChanged;

            _viewModel.ProgressBarVisibility = Visibility.Hidden;
        }

        private void ProgressBar_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_progressBarStoryboard == null)
            {
                return;
            }

            if (ProgressBar.IsVisible)
            {
                _progressBarStoryboard.Begin(ProgressBar, true);
            }
            else
            {
                _progressBarStoryboard.Stop(ProgressBar);
            }
        }

        private void WindowAnimation()
        {
            _isArrowKeyPressed = true;

            var clocksb = new Storyboard();
            var iconsb = new Storyboard();
            var easing = new CircleEase { EasingMode = EasingMode.EaseInOut };

            var animationLength = _settings.AnimationSpeed switch
            {
                AnimationSpeeds.Slow => 560,
                AnimationSpeeds.Medium => 360,
                AnimationSpeeds.Fast => 160,
                _ => _settings.CustomAnimationLength
            };

            var IconMotion = new DoubleAnimation
            {
                From = 12,
                To = 0,
                EasingFunction = easing,
                Duration = TimeSpan.FromMilliseconds(animationLength),
                FillBehavior = FillBehavior.HoldEnd
            };

            var ClockOpacity = new DoubleAnimation
            {
                From = 0,
                To = 1,
                EasingFunction = easing,
                Duration = TimeSpan.FromMilliseconds(animationLength),
                FillBehavior = FillBehavior.HoldEnd
            };

            var TargetIconOpacity = GetOpacityFromStyle(SearchIcon.Style, 1.0);

            var IconOpacity = new DoubleAnimation
            {
                From = 0,
                To = TargetIconOpacity,
                EasingFunction = easing,
                Duration = TimeSpan.FromMilliseconds(animationLength),
                FillBehavior = FillBehavior.HoldEnd
            };

            var rightMargin = GetThicknessFromStyle(ClockPanel.Style, new Thickness(0, 0, DefaultRightMargin, 0)).Right;

            var thicknessAnimation = new ThicknessAnimation
            {
                From = new Thickness(0, 12, rightMargin, 0),
                To = new Thickness(0, 0, rightMargin, 0),
                EasingFunction = easing,
                Duration = TimeSpan.FromMilliseconds(animationLength),
                FillBehavior = FillBehavior.HoldEnd
            };

            Storyboard.SetTarget(ClockOpacity, ClockPanel);
            Storyboard.SetTargetProperty(ClockOpacity, new PropertyPath(OpacityProperty));

            Storyboard.SetTarget(thicknessAnimation, ClockPanel);
            Storyboard.SetTargetProperty(thicknessAnimation, new PropertyPath(MarginProperty));

            Storyboard.SetTarget(IconMotion, SearchIcon);
            Storyboard.SetTargetProperty(IconMotion, new PropertyPath(TopProperty));

            Storyboard.SetTarget(IconOpacity, SearchIcon);
            Storyboard.SetTargetProperty(IconOpacity, new PropertyPath(OpacityProperty));

            clocksb.Children.Add(thicknessAnimation);
            clocksb.Children.Add(ClockOpacity);
            iconsb.Children.Add(IconMotion);
            iconsb.Children.Add(IconOpacity);

            _settings.WindowLeft = Left;
            _isArrowKeyPressed = false;

            clocksb.Begin(ClockPanel);
            iconsb.Begin(SearchIcon);
        }

        private void UpdateClockPanelVisibility()
        {
            if (QueryTextBox == null || ResultContextMenu == null || History == null || ClockPanel == null)
            {
                return;
            }

            // ✅ Initialize animation length & duration
            var animationLength = _settings.AnimationSpeed switch
            {
                AnimationSpeeds.Slow => 560,
                AnimationSpeeds.Medium => 360,
                AnimationSpeeds.Fast => 160,
                _ => _settings.CustomAnimationLength
            };
            var animationDuration = TimeSpan.FromMilliseconds(animationLength * 2 / 3);

            // ✅ Conditions for showing ClockPanel (No query input / ResultContextMenu & History are closed)
            var shouldShowClock = QueryTextBox.Text.Length == 0 &&
                ResultContextMenu.Visibility != Visibility.Visible &&
                History.Visibility != Visibility.Visible;

            // ✅ 1. When ResultContextMenu opens, immediately set Visibility.Hidden (force hide without animation)
            if (ResultContextMenu.Visibility == Visibility.Visible)
            {
                _viewModel.ClockPanelVisibility = Visibility.Hidden;
                _viewModel.ClockPanelOpacity = 0.0;  // Set to 0 in case Opacity animation affects it
                return;
            }

            // ✅ 2. When ResultContextMenu is closed, keep it Hidden if there's text in the query (remember previous state)
            else if (QueryTextBox.Text.Length > 0)
            {
                _viewModel.ClockPanelVisibility = Visibility.Hidden;
                _viewModel.ClockPanelOpacity = 0.0;
                return;
            }

            // ✅ Prevent multiple animations
            if (_isClockPanelAnimating)
            {
                return;
            }

            // ✅ 3. When hiding ClockPanel (apply fade-out animation)
            if ((!shouldShowClock) && _viewModel.ClockPanelVisibility == Visibility.Visible)
            {
                _isClockPanelAnimating = true;

                var fadeOut = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = animationDuration,
                    FillBehavior = FillBehavior.HoldEnd
                };

                fadeOut.Completed += (s, e) =>
                {
                    _viewModel.ClockPanelVisibility = Visibility.Hidden; // ✅ Completely hide after animation
                    _isClockPanelAnimating = false;
                };

                ClockPanel.BeginAnimation(OpacityProperty, fadeOut);
            }

            // ✅ 4. When showing ClockPanel (apply fade-in animation)
            else if (shouldShowClock && _viewModel.ClockPanelVisibility != Visibility.Visible)
            {
                _isClockPanelAnimating = true;

                _viewModel.ClockPanelVisibility = Visibility.Visible;  // ✅ Set Visibility to Visible first

                var fadeIn = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = animationDuration,
                    FillBehavior = FillBehavior.HoldEnd
                };

                fadeIn.Completed += (s, e) => _isClockPanelAnimating = false;

                ClockPanel.BeginAnimation(OpacityProperty, fadeIn);
            }
        }

        private static double GetOpacityFromStyle(Style style, double defaultOpacity = 1.0)
        {
            if (style == null)
            {
                return defaultOpacity;
            }

            foreach (Setter setter in style.Setters.Cast<Setter>())
            {
                if (setter.Property == OpacityProperty)
                {
                    return setter.Value is double opacity ? opacity : defaultOpacity;
                }
            }

            return defaultOpacity;
        }

        private static Thickness GetThicknessFromStyle(Style style, Thickness defaultThickness)
        {
            if (style == null)
            {
                return defaultThickness;
            }

            foreach (Setter setter in style.Setters.Cast<Setter>())
            {
                if (setter.Property == MarginProperty)
                {
                    return setter.Value is Thickness thickness ? thickness : defaultThickness;
                }
            }

            return defaultThickness;
        }

        #endregion
    }
}
