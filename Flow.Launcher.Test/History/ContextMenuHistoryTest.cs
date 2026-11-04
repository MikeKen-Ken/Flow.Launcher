using System.Text.Json;
using Flow.Launcher.History;
using Flow.Launcher.Plugin;
using Flow.Launcher.Storage;
using NUnit.Framework;

namespace Flow.Launcher.Test.History;

[TestFixture]
public class ContextMenuHistoryTest
{
    [Test]
    public void TryCreate_KeepsSourceIdentityAndMenuLabel()
    {
        var source = FileResult();
        var menu = new Result
        {
            Title = "Run as administrator",
            PluginID = "program",
            OriginQuery = source.OriginQuery
        };

        var recorded = ContextMenuHistory.TryCreate(source, menu);

        Assert.Multiple(() =>
        {
            Assert.That(recorded, Is.Not.Null);
            Assert.That(recorded.Title, Is.EqualTo("notes.exe"));
            Assert.That(recorded.SubTitle, Is.EqualTo(@"C:\apps"));
            Assert.That(recorded.RecordKey, Is.EqualTo(@"C:\apps\notes.exe"));
            Assert.That(recorded.HistoryAction.Label, Is.EqualTo("Run as administrator"));
            Assert.That(recorded.HistoryAction.Id, Is.EqualTo(ContextMenuHistory.ActionIdPrefix + "Run as administrator"));
            Assert.That(recorded.HistoryAction.ReplayMode, Is.EqualTo(HistoryReplayMode.Execute));
        });
    }

    [Test]
    public void TryCreate_UsesPluginActionWhenTheMenuDefinesOne()
    {
        var source = FileResult();
        var menu = new Result
        {
            Title = "Delete",
            PluginID = "explorer",
            OriginQuery = source.OriginQuery,
            HistoryAction = new HistoryActionDescriptor
            {
                Id = "explorer.delete",
                Label = "Delete",
                Kind = HistoryActionKind.Destructive,
                ReplayMode = HistoryReplayMode.ShowQuery
            }
        };

        var recorded = ContextMenuHistory.TryCreate(source, menu);

        Assert.Multiple(() =>
        {
            Assert.That(recorded.HistoryAction.Id, Is.EqualTo("explorer.delete"));
            Assert.That(recorded.HistoryAction.Kind, Is.EqualTo(HistoryActionKind.Destructive));
            Assert.That(recorded.HistoryAction.ReplayMode, Is.EqualTo(HistoryReplayMode.ShowQuery));
        });
    }

    [Test]
    public void TryCreate_SkipsHostMenuRows()
    {
        var source = FileResult();
        var settings = new Result
        {
            Title = "Plugin settings",
            OriginQuery = source.OriginQuery
        };

        Assert.That(ContextMenuHistory.TryCreate(source, settings), Is.Null);
    }

    [Test]
    public void Add_ContextMenuAction_StaysSeparateFromOpeningTheFile()
    {
        var history = new Storage.History();
        var source = FileResult();
        var runAsAdmin = ContextMenuHistory.TryCreate(source, Menu("Run as administrator"));
        var openFolder = ContextMenuHistory.TryCreate(source, Menu("Open containing folder"));

        Assert.Multiple(() =>
        {
            Assert.That(history.Add(source), Is.True);
            Assert.That(history.Add(runAsAdmin, fromContextMenu: true), Is.True);
            Assert.That(history.Add(openFolder, fromContextMenu: true), Is.True);
            Assert.That(history.Add(runAsAdmin, fromContextMenu: true), Is.True);
            Assert.That(history.LastOpenedHistoryItems, Has.Count.EqualTo(3));
        });

        var savedRun = history.LastOpenedHistoryItems[2];
        Assert.Multiple(() =>
        {
            Assert.That(savedRun.Title, Is.EqualTo("notes.exe"));
            Assert.That(savedRun.Provenance.FromContextMenu, Is.True);
            Assert.That(savedRun.Provenance.ActionLabel, Is.EqualTo("Run as administrator"));
            Assert.That(history.LastOpenedHistoryItems[0].Provenance.FromContextMenu, Is.False);
        });
    }

    [Test]
    public void FindMenuItem_MatchesStableIdThenTitle()
    {
        var menus = new[]
        {
            new Result { Title = "Open containing folder" },
            new Result
            {
                Title = "Run as administrator",
                HistoryAction = new HistoryActionDescriptor
                {
                    Id = "program.run-as-administrator",
                    Label = "Run as administrator"
                }
            }
        };

        var byId = ContextMenuHistory.FindMenuItem(menus, new HistoryProvenance
        {
            ActionId = "program.run-as-administrator",
            ActionLabel = "Run elevated"
        });
        var byTitle = ContextMenuHistory.FindMenuItem(menus, new HistoryProvenance
        {
            ActionId = ContextMenuHistory.ActionIdPrefix + "Open containing folder",
            ActionLabel = "Open containing folder"
        });

        Assert.Multiple(() =>
        {
            Assert.That(byId.Title, Is.EqualTo("Run as administrator"));
            Assert.That(byTitle.Title, Is.EqualTo("Open containing folder"));
        });
    }

    [Test]
    public void Provenance_ContextMenuFlagSurvivesJsonAndCopy()
    {
        var history = new Storage.History();
        var recorded = ContextMenuHistory.TryCreate(FileResult(), Menu("Run as administrator"));
        history.Add(recorded, fromContextMenu: true);
        var stored = history.LastOpenedHistoryItems[0];

        var restored = JsonSerializer.Deserialize<LastOpenedHistoryResult>(JsonSerializer.Serialize(stored));
        var copy = stored.Provenance.DeepCopy();

        Assert.Multiple(() =>
        {
            Assert.That(restored.Provenance.FromContextMenu, Is.True);
            Assert.That(restored.Provenance.ActionLabel, Is.EqualTo("Run as administrator"));
            Assert.That(copy.FromContextMenu, Is.True);
            Assert.That(copy.ActionId, Is.EqualTo(stored.Provenance.ActionId));
        });
    }

    private static Result FileResult()
    {
        return new Result
        {
            Title = "notes.exe",
            SubTitle = @"C:\apps",
            PluginID = "program",
            RecordKey = @"C:\apps\notes.exe",
            OriginQuery = new Query
            {
                OriginalQuery = "notes",
                TrimmedQuery = "notes",
                Search = "notes",
                SearchTerms = ["notes"]
            }
        };
    }

    private static Result Menu(string title)
    {
        return new Result
        {
            Title = title,
            PluginID = "program",
            OriginQuery = new Query { TrimmedQuery = "notes", Search = "notes" }
        };
    }
}
