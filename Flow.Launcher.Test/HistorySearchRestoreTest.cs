using System.Reflection;
using System.Runtime.CompilerServices;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Storage;
using Flow.Launcher.ViewModel;
using Moq;
using NUnit.Framework;

namespace Flow.Launcher.Test;

public class HistorySearchRestoreTest
{
    private IPublicAPI _previousApi;

    [SetUp]
    public void SetUp()
    {
        _previousApi = App.API;
    }

    [TearDown]
    public void TearDown()
    {
        typeof(App).GetProperty(nameof(App.API))!.SetValue(null, _previousApi);
    }

    [Test]
    public void HistoryCopy_UsesStoredQueryText()
    {
        var api = new Mock<IPublicAPI>();
        SetApi(api.Object);
        var history = new LastOpenedHistoryResult
        {
            PluginID = string.Empty,
            Query = "notepad query",
            SourceHistoryItem = new LastOpenedHistoryResult
            {
                PluginID = "program",
                Query = "rebuilt keyword query"
            }
        };

        var restored = ResultHelper.TryRestoreHistorySearch(history);

        Assert.That(restored, Is.True);
        api.Verify(a => a.BackToQueryResults(), Times.Once);
        api.Verify(a => a.ChangeQuery("notepad query", false), Times.Once);
        api.Verify(a => a.ChangeQuery("rebuilt keyword query", false), Times.Never);
    }

    [Test]
    public void PluginResult_LeavesContextMenuPath()
    {
        var api = new Mock<IPublicAPI>();
        SetApi(api.Object);

        var restored = ResultHelper.TryRestoreHistorySearch(new Result
        {
            PluginID = "program",
            Title = "Notepad"
        });

        Assert.That(restored, Is.False);
        api.Verify(a => a.ChangeQuery(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        api.Verify(a => a.BackToQueryResults(), Times.Never);
    }

    [Test]
    public void RightArrow_SelectedHistoryRow_UsesStoredQueryText()
    {
        var api = new Mock<IPublicAPI>();
        SetApi(api.Object);
        var settings = new Settings();
        var results = (ResultsViewModel)RuntimeHelpers.GetUninitializedObject(typeof(ResultsViewModel));
        results.SelectedItem = new ResultViewModel(new LastOpenedHistoryResult
        {
            PluginID = string.Empty,
            Query = "calc"
        }, settings);
        var viewModel = (MainViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainViewModel));
        typeof(MainViewModel).GetField("_selectedResults", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewModel, results);

        var restored = viewModel.RestoreSelectedHistorySearch();

        Assert.That(restored, Is.True);
        api.Verify(a => a.ChangeQuery("calc", false), Times.Once);
    }

    private static void SetApi(IPublicAPI api)
    {
        typeof(App).GetProperty(nameof(App.API), BindingFlags.Static | BindingFlags.Public)!
            .SetValue(null, api);
    }
}
