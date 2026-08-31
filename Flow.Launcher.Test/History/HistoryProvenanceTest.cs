using System.Text.Json;
using Flow.Launcher.History;
using Flow.Launcher.Plugin;
using Flow.Launcher.Storage;
using NUnit.Framework;

namespace Flow.Launcher.Test.History;

[TestFixture]
public class HistoryProvenanceTest
{
    [Test]
    public void Capture_RecordsHostSourceAndOptionalAction()
    {
        var result = new Result
        {
            PluginID = "process-killer",
            OriginQuery = CreateQuery("ki notepad.exe", "ki", "notepad.exe"),
            HistoryAction = new HistoryActionDescriptor
            {
                Id = "process.kill-one",
                Label = "Kill process",
                Kind = HistoryActionKind.Destructive,
                ReplayMode = HistoryReplayMode.ShowQuery
            }
        };
        var metadata = new PluginMetadata
        {
            ID = "process-killer",
            Name = "Process Killer",
            IcoPath = "Images/app.png"
        };

        var provenance = HistoryProvenance.Capture(result, metadata);

        Assert.Multiple(() =>
        {
            Assert.That(provenance.PluginName, Is.EqualTo("Process Killer"));
            Assert.That(provenance.ActionKeyword, Is.EqualTo("ki"));
            Assert.That(provenance.SearchText, Is.EqualTo("notepad.exe"));
            Assert.That(provenance.ActionId, Is.EqualTo("process.kill-one"));
            Assert.That(provenance.ActionKind, Is.EqualTo(HistoryActionKind.Destructive));
            Assert.That(provenance.ReplayMode, Is.EqualTo(HistoryReplayMode.ShowQuery));
        });
    }

    [Test]
    public void BuildQuery_UsesCurrentKeywordAndSavedSearchText()
    {
        var item = new LastOpenedHistoryResult
        {
            Query = "ki notepad.exe",
            Provenance = new HistoryProvenance
            {
                ActionKeyword = "ki",
                SearchText = "notepad.exe"
            }
        };
        var metadata = new PluginMetadata
        {
            ActionKeyword = "kill",
            ActionKeywords = ["kill"]
        };

        var query = HistoryReplay.BuildQuery(item, metadata);

        Assert.Multiple(() =>
        {
            Assert.That(query.TrimmedQuery, Is.EqualTo("kill notepad.exe"));
            Assert.That(query.ActionKeyword, Is.EqualTo("kill"));
            Assert.That(query.Search, Is.EqualTo("notepad.exe"));
        });
    }

    [Test]
    public void BuildQuery_PreservesSavedKeywordWhenItIsStillRegistered()
    {
        var item = new LastOpenedHistoryResult
        {
            Query = "g example",
            Provenance = new HistoryProvenance
            {
                ActionKeyword = "g",
                SearchText = "example"
            }
        };
        var metadata = new PluginMetadata
        {
            ActionKeyword = "ddg",
            ActionKeywords = ["ddg", "g"]
        };

        var query = HistoryReplay.BuildQuery(item, metadata);

        Assert.Multiple(() =>
        {
            Assert.That(query.TrimmedQuery, Is.EqualTo("g example"));
            Assert.That(query.ActionKeyword, Is.EqualTo("g"));
            Assert.That(query.Search, Is.EqualTo("example"));
        });
    }

    [Test]
    public void BuildQuery_UsesReplacementKeywordWhenSavedKeywordWasRemoved()
    {
        var item = new LastOpenedHistoryResult
        {
            Query = "g example",
            Provenance = new HistoryProvenance
            {
                ActionKeyword = "g",
                SearchText = "example"
            }
        };
        var metadata = new PluginMetadata
        {
            ActionKeyword = "search",
            ActionKeywords = ["search"]
        };

        var queryText = HistoryReplay.BuildQueryText(item, metadata);

        Assert.That(queryText, Is.EqualTo("search example"));
    }

    [Test]
    public void IsSemanticMatch_RejectsDifferentActionsFromSamePlugin()
    {
        var item = new LastOpenedHistoryResult
        {
            Provenance = new HistoryProvenance { ActionId = "process.kill-one" }
        };
        var result = new Result
        {
            HistoryAction = new HistoryActionDescriptor
            {
                Id = "process.kill-all",
                Label = "Kill all"
            }
        };

        Assert.That(HistoryReplay.IsSemanticMatch(item, result), Is.False);
    }

    [Test]
    public void LastOpenedHistoryResult_ProvenanceSurvivesJsonRoundTrip()
    {
        var item = new LastOpenedHistoryResult
        {
            PluginID = "process-killer",
            Query = "ki notepad.exe",
            Provenance = new HistoryProvenance
            {
                PluginName = "Process Killer",
                ActionKeyword = "ki",
                SearchText = "notepad.exe",
                ActionId = "process.kill-one",
                ActionLabel = "Kill process",
                ActionKind = HistoryActionKind.Destructive,
                ReplayMode = HistoryReplayMode.ShowQuery
            }
        };

        var serialized = JsonSerializer.Serialize(item);
        var restored = JsonSerializer.Deserialize<LastOpenedHistoryResult>(serialized);

        Assert.Multiple(() =>
        {
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.PluginID, Is.EqualTo("process-killer"));
            Assert.That(restored.Provenance, Is.Not.Null);
            Assert.That(restored.Provenance!.PluginName, Is.EqualTo("Process Killer"));
            Assert.That(restored.Provenance.ActionId, Is.EqualTo("process.kill-one"));
            Assert.That(restored.Provenance.ActionKind, Is.EqualTo(HistoryActionKind.Destructive));
        });
    }

    [Test]
    public void LegacyJsonWithoutProvenance_RemainsReadable()
    {
        const string json = """
            {
              "Title": "notepad.exe",
              "PluginID": "process-killer",
              "Query": "ki notepad.exe"
            }
            """;

        var restored = JsonSerializer.Deserialize<LastOpenedHistoryResult>(json);

        Assert.Multiple(() =>
        {
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.Query, Is.EqualTo("ki notepad.exe"));
            Assert.That(restored.Provenance, Is.Null);
        });
    }

    private static Query CreateQuery(string query, string actionKeyword, string search)
    {
        return new Query
        {
            OriginalQuery = query,
            TrimmedQuery = query,
            ActionKeyword = actionKeyword,
            Search = search,
            SearchTerms = search.Split(Query.TermSeparator)
        };
    }
}
