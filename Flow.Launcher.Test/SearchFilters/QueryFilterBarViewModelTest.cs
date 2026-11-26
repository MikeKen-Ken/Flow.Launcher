using System.Linq;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.SearchFilters;
using NUnit.Framework;

namespace Flow.Launcher.Test.SearchFilters;

public class QueryFilterBarViewModelTest
{
    [Test]
    [CancelAfter(2000)]
    public void Constructor_DoesNotRequireLocalizationOrPublicApi()
    {
        var settings = new Settings();

        var vm = new QueryFilterBarViewModel(settings, () => string.Empty, _ => { });

        Assert.That(vm.Filters.Count, Is.EqualTo(11));
        Assert.That(vm.QuickExtensions.Select(item => item.Label), Is.EqualTo(new[] { ".exe", ".lnk" }));
        Assert.That(vm.Menu.IsSelected, Is.False);
        Assert.That(vm.IsVisible, Is.True);
        foreach (var filter in vm.Filters)
        {
            Assert.That(filter.DisplayText, Is.Empty);
        }
    }

    [Test]
    public void PinnedExtensions_ToggleTogetherAndLeaveThePanelIdle()
    {
        var query = string.Empty;
        var vm = new QueryFilterBarViewModel(new Settings(), () => query, next => query = next);

        vm.ToggleQuickExtension("exe");
        vm.ToggleQuickExtension("lnk");
        vm.SyncQuickSelection(query);

        Assert.That(query, Is.EqualTo("ext:exe;lnk"));
        Assert.That(vm.QuickExtensions.Select(item => item.IsSelected), Is.EqualTo(new[] { true, true }));
        Assert.That(vm.Menu.IsSelected, Is.False);

        vm.ToggleQuickExtension("exe");
        vm.SyncQuickSelection(query);

        Assert.That(query, Is.EqualTo("ext:lnk"));
        Assert.That(vm.QuickExtensions.Single(item => item.Extension == "exe").IsSelected, Is.False);
        Assert.That(vm.QuickExtensions.Single(item => item.Extension == "lnk").IsSelected, Is.True);
    }

    [Test]
    public void UnpinnedFilter_MarksTheFilterPanel()
    {
        var query = "notes ext:png;exe file:";
        var vm = new QueryFilterBarViewModel(new Settings(), () => query, next => query = next);

        vm.SyncQuickSelection(query);

        Assert.That(vm.QuickExtensions.Single(item => item.Extension == "exe").IsSelected, Is.True);
        Assert.That(vm.QuickExtensions.Single(item => item.Extension == "lnk").IsSelected, Is.False);
        Assert.That(vm.Menu.IsSelected, Is.True);
    }
}
