using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Flow.Launcher.SearchFilters;
using NUnit.Framework;

namespace Flow.Launcher.Test.SearchFilters;

[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class QueryFilterExtensionPickerFocusTest
{
    [Test]
    public void InputClick_ReactivatesOwnerAfterAnotherWindowTakesFocus()
    {
        var picker = new QueryFilterExtensionPicker();
        var input = (TextBox)picker.FindName("CustomExtensionInput");
        var popup = new Popup { Child = picker, StaysOpen = true };
        var owner = new Window { Content = popup, Width = 200, Height = 100 };
        var other = new Window { Width = 200, Height = 100 };
        try
        {
            owner.Show();
            popup.IsOpen = true;
            other.Show();
            other.Activate();
            Pump();
            Assert.That(owner.IsActive, Is.False, "Repro requires an inactive popup owner");
            input.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent
            });
            Pump();
            Assert.That(owner.IsActive, Is.True, "Clicking the popup input must activate its owner");
            Assert.That(input.IsKeyboardFocused, Is.True, "Typing must reach the input without reopening");
            input.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,
                new TextComposition(InputManager.Current, input, "log"))
            {
                RoutedEvent = TextCompositionManager.TextInputEvent
            });
            Assert.That(input.Text, Is.EqualTo("log"));
        }
        finally
        {
            popup.IsOpen = false;
            other.Close();
            owner.Close();
        }
    }

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
}
