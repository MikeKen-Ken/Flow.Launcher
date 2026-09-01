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

        Assert.That(vm.Filters.Count, Is.EqualTo(9));
        Assert.That(vm.IsVisible, Is.True);
        foreach (var filter in vm.Filters)
        {
            Assert.That(filter.DisplayText, Is.Empty);
        }
    }
}
