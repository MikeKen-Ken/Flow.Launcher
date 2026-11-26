using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.SearchFilters;
using NUnit.Framework;

namespace Flow.Launcher.Test.SearchFilters;

[Apartment(ApartmentState.STA)]
public class QueryFilterBarLayoutTest
{
    [Test]
    public void PinnedChips_StayOnTheBarAndTheOtherFiltersOpenInAPanel()
    {
        var query = string.Empty;
        var vm = new QueryFilterBarViewModel(new Settings(), () => query, next => query = next);
        var bar = new QueryFilterBar { DataContext = vm, Width = 720, Height = 72 };
        var window = new Window { Content = bar, Width = 800, Height = 240, ShowInTaskbar = false };

        try
        {
            window.Show();
            Pump();

            var barButtons = FindButtons(bar).ToList();
            Assert.That(barButtons.Count(button => button.DataContext is QueryFilterQuickExtensionViewModel), Is.EqualTo(2));

            Click(barButtons.Single(button => button.DataContext is QueryFilterQuickExtensionViewModel { Extension: "exe" }));
            Click(barButtons.Single(button => button.DataContext is QueryFilterQuickExtensionViewModel { Extension: "lnk" }));
            vm.SyncQuickSelection(query);
            Pump();

            Assert.That(query, Is.EqualTo("ext:exe;lnk"));
            Assert.That(vm.QuickExtensions.All(item => item.IsSelected), Is.True);

            var filters = (Button)bar.FindName("FiltersButton");
            Click(filters);
            Pump();

            var popup = (Popup)bar.FindName("FiltersPopup");
            Assert.That(popup.IsOpen, Is.True);
            var panelButtons = FindButtons((DependencyObject)bar.FindName("FiltersPanel"))
                .Where(button => button.DataContext is QueryFilterItemViewModel)
                .ToList();
            Assert.That(panelButtons, Has.Count.EqualTo(11));
            Assert.That(panelButtons.Any(button => button.DataContext is QueryFilterItemViewModel { Id: QueryFilterId.Extension }), Is.True);
        }
        finally
        {
            window.Close();
        }
    }

    private static void Click(Button button) =>
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static IEnumerable<Button> FindButtons(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is Button button)
            {
                yield return button;
            }

            foreach (var nested in FindButtons(child))
            {
                yield return nested;
            }
        }
    }

    private static void Pump() =>
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
}
