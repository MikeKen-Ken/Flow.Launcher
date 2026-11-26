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
    public void OriginalFiltersStayOnTheBarAndThePanelOnlyHasExeAndLnk()
    {
        var query = string.Empty;
        var vm = new QueryFilterBarViewModel(new Settings(), () => query, next => query = next);
        var bar = new QueryFilterBar { DataContext = vm, Width = 720, Height = 160 };
        var window = new Window { Content = bar, Width = 800, Height = 320, ShowInTaskbar = false };

        try
        {
            window.Show();
            Pump();

            var barOrder = FindButtons(bar)
                .Select(Describe)
                .Where(name => name is not null)
                .ToList();
            Assert.That(barOrder, Is.EqualTo(new[]
            {
                "Path", "File", "Folder", "Extension", "NameMatch", "CaseSensitive", "Menu"
            }));

            Click(FindButtons(bar).Single(button => button.DataContext is QueryFilterMenuState));
            Pump();

            var popup = (Popup)bar.FindName("FiltersPopup");
            var panel = (DependencyObject)bar.FindName("FiltersPanel");
            Assert.That(popup.IsOpen, Is.True);
            var panelOrder = FindButtons(panel).Select(Describe).Where(name => name is not null).ToList();
            Assert.That(panelOrder, Is.EqualTo(new[]
            {
                "exe", "lnk", "Size", "DateModified", "DateCreated", "DateAccessed", "Hidden"
            }));

            Click(FindButtons(panel).Single(button => button.DataContext is QueryFilterQuickExtensionViewModel { Extension: "exe" }));
            Click(FindButtons(panel).Single(button => button.DataContext is QueryFilterQuickExtensionViewModel { Extension: "lnk" }));
            vm.SyncQuickSelection(query);
            Pump();

            Assert.That(query, Is.EqualTo("ext:exe;lnk"));
            Assert.That(vm.QuickExtensions.All(item => item.IsSelected), Is.True);
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

    private static string Describe(Button button) => button.DataContext switch
    {
        QueryFilterItemViewModel filter => filter.Id.ToString(),
        QueryFilterMenuState => "Menu",
        QueryFilterQuickExtensionViewModel quick => quick.Extension,
        _ => null
    };

    private static void Pump() =>
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
}
