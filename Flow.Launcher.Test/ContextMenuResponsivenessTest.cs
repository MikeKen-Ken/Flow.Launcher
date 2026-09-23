using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.Storage;
using Flow.Launcher.ViewModel;
using Moq;
using NUnit.Framework;

namespace Flow.Launcher.Test;

[Apartment(ApartmentState.STA)]
[NonParallelizable]
[SingleThreaded]
public class ContextMenuResponsivenessTest
{
    private readonly List<Task> _queries = new();
    private Mock<IAsyncPlugin> _plugin;
    private Mock<IPublicAPI> _api;
    private IPublicAPI _previousApi;
    private PluginPair[] _previousPlugins;
    private MainViewModel _vm;
    private SynchronizationContext _previousContext;
    private static ConcurrentBag<PluginPair> Plugins => (ConcurrentBag<PluginPair>)typeof(PluginManager)
        .GetField("_contextMenuPlugins", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
    private static ConcurrentDictionary<string, PluginPair> LoadedPlugins => (ConcurrentDictionary<string, PluginPair>)typeof(PluginManager)
        .GetField("_allLoadedPlugins", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
    private const string PluginId = "menu-responsiveness-test";

    [OneTimeSetUp]
    public void InitializeTranslations()
    {
        var services = new Mock<IServiceProvider>();
        services.Setup(p => p.GetService(typeof(IPublicAPI))).Returns(() => App.API);
        Ioc.Default.ConfigureServices(services.Object);
    }

    [SetUp]
    public void SetUp()
    {
        _previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        _previousApi = App.API;
        _api = new Mock<IPublicAPI>();
        _api.Setup(a => a.GetTranslation(It.IsAny<string>())).Returns((string key) => key);
        _api.Setup(a => a.FuzzySearch(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string query, string text) => new MatchResult(true, SearchPrecisionScore.Regular)
            { RawScore = text?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ? 100 : 0 });
        typeof(App).GetProperty(nameof(App.API))!.SetValue(null, _api.Object);
        _plugin = new Mock<IAsyncPlugin>();
        _plugin.As<IContextMenu>().Setup(p => p.LoadContextMenus(It.IsAny<Result>()))
            .Returns(() => new List<Result> { new() { Title = "Open" }, new() { Title = "Copy" } });
        var pair = new PluginPair
        {
            Plugin = _plugin.Object,
            Metadata = new PluginMetadata
            {
                ID = PluginId, Name = "Test", ExecuteFileName = "test.dll",
                IcoPath = "test.png", PluginDirectory = AppContext.BaseDirectory
            }
        };
        _previousPlugins = Plugins.ToArray();
        Plugins.Clear();
        Plugins.Add(pair);
        LoadedPlugins[PluginId] = pair;
        _vm = (MainViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainViewModel));
        var settings = new Settings();
        var topMost = (FlowLauncherJsonStorageTopMostRecord)RuntimeHelpers.GetUninitializedObject(typeof(FlowLauncherJsonStorageTopMostRecord));
        SetField(topMost, "_topMostRecord", new MultipleTopMostRecord());
        SetField(_vm, "_topMostRecord", topMost);
        SetField(_vm, "<Settings>k__BackingField", settings);
        SetField(_vm, "<Results>k__BackingField", new ResultsViewModel
        {
            SelectedItem = new ResultViewModel(new Result { PluginID = PluginId }, settings)
        });
        SetField(_vm, "<ContextMenu>k__BackingField", new ResultsViewModel());
        SetField(_vm, "<History>k__BackingField", new ResultsViewModel());
        SetField(_vm, "_selectedResults", _vm.ContextMenu);
        SetField(_vm, "_queryText", string.Empty);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var query in _queries) Complete(query);
        _queries.Clear();
        Plugins.Clear();
        foreach (var plugin in _previousPlugins) Plugins.Add(plugin);
        LoadedPlugins.TryRemove(PluginId, out _);
        typeof(App).GetProperty(nameof(App.API))!.SetValue(null, _previousApi);
        SynchronizationContext.SetSynchronizationContext(_previousContext);
        _api.Verify(a => a.LogException(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void SlowPlugin_DoesNotBlockMenuQueryCaller()
    {
        var caller = Environment.CurrentManagedThreadId;
        var worker = caller;
        var apartment = ApartmentState.Unknown;
        _plugin.As<IContextMenu>().Setup(p => p.LoadContextMenus(It.IsAny<Result>())).Returns(() =>
        {
            worker = Environment.CurrentManagedThreadId;
            apartment = Thread.CurrentThread.GetApartmentState();
            Thread.Sleep(500);
            return new List<Result> { new() { Title = "Open" } };
        });
        var watch = Stopwatch.StartNew();
        _vm.Query(false);
        watch.Stop();
        TestContext.Out.WriteLine($"Menu query returned in {watch.ElapsedMilliseconds} ms with a 500 ms plugin");
        Assert.That(watch.ElapsedMilliseconds, Is.LessThan(200));
        Assert.That(_vm.ContextMenu.Results.Single().Result.Title, Is.EqualTo("pleaseWait"));
        Complete(QueryAsync());
        Assert.That(_vm.ContextMenu.Results.Any(r => r.Result.Title == "Open"), Is.True);
        Assert.That(worker, Is.Not.EqualTo(caller));
        Assert.That(apartment, Is.EqualTo(ApartmentState.STA));
        _plugin.As<IContextMenu>().Verify(p => p.LoadContextMenus(It.IsAny<Result>()), Times.Once);
    }

    [Test]
    public void Filtering_ReusesMenuAndClearsSelectionWhenNothingMatches()
    {
        Complete(QueryAsync());
        SetField(_vm, "_queryText", "copy");
        Complete(QueryAsync());
        Assert.That(_vm.ContextMenu.Results.Single().Result.Title, Is.EqualTo("Copy"));
        SetField(_vm, "_queryText", "no matching action");
        Complete(QueryAsync());
        Assert.That(_vm.ContextMenu.Results, Is.Empty);
        Assert.That(_vm.ContextMenu.SelectedItem, Is.Null);
        _plugin.As<IContextMenu>().Verify(p => p.LoadContextMenus(It.IsAny<Result>()), Times.Once);
    }

    [Test]
    public void FilterChangedDuringLoad_OnlyLatestFilterIsDisplayed()
    {
        using var release = new ManualResetEventSlim();
        _plugin.As<IContextMenu>().Setup(p => p.LoadContextMenus(It.IsAny<Result>())).Returns(() =>
        {
            release.Wait(TimeSpan.FromSeconds(5));
            return new List<Result> { new() { Title = "Open" }, new() { Title = "Copy" } };
        });
        var first = QueryAsync();
        SetField(_vm, "_queryText", "copy");
        var latest = QueryAsync();
        release.Set();
        Complete(Task.WhenAll(first, latest));
        Assert.That(_vm.ContextMenu.Results.Single().Result.Title, Is.EqualTo("Copy"));
        _plugin.As<IContextMenu>().Verify(p => p.LoadContextMenus(It.IsAny<Result>()), Times.Once);
    }

    [Test]
    public void LeavingMenu_DiscardsLateResults()
    {
        using var release = new ManualResetEventSlim();
        _plugin.As<IContextMenu>().Setup(p => p.LoadContextMenus(It.IsAny<Result>())).Returns(() =>
        {
            release.Wait(TimeSpan.FromSeconds(5));
            return new List<Result> { new() { Title = "Stale" } };
        });
        var pending = QueryAsync();
        SetField(_vm, "_selectedResults", _vm.Results);
        release.Set();
        Complete(pending);
        Assert.That(_vm.ContextMenu.Results.Any(r => r.Result.Title == "Stale"), Is.False);
    }

    [Test]
    public void ReopeningSameResult_StartsFreshLoadAndDiscardsPreviousSession()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var calls = 0;
        _plugin.As<IContextMenu>().Setup(p => p.LoadContextMenus(It.IsAny<Result>())).Returns(() =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(5));
                return new List<Result> { new() { Title = "Stale" } };
            }
            return new List<Result> { new() { Title = "Fresh" } };
        });
        var previous = QueryAsync();
        try
        {
            Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            // Exercise the real navigation setter and right-click command.
            typeof(MainViewModel).GetProperty("SelectedResults", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(_vm, _vm.Results);
            Complete(_vm.LoadContextMenuCommand.ExecuteAsync(null));
            Complete(QueryAsync());
            Assert.That(_vm.ContextMenu.Results.Any(r => r.Result.Title == "Fresh"), Is.True);
        }
        finally
        {
            release.Set();
            Complete(previous);
        }
        Assert.That(_vm.ContextMenu.Results.Any(r => r.Result.Title == "Stale"), Is.False);
        Assert.That(calls, Is.EqualTo(2));
    }

    private Task QueryAsync()
    {
        var task = (Task)typeof(MainViewModel).GetMethod("QueryAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(_vm, new object[] { false, false })!;
        _queries.Add(task);
        return task;
    }

    private static void Complete(Task task)
    {
        // Pump the STA dispatcher while observing an async UI operation.
#pragma warning disable VSTHRD001, VSTHRD002
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(10))
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Thread.Sleep(1);
        }
        Assert.That(task.IsCompleted, Is.True, "Menu query did not complete");
        task.GetAwaiter().GetResult();
#pragma warning restore VSTHRD001, VSTHRD002
    }

    private static void SetField(object instance, string name, object value) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);
}
