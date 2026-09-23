using System;
using Flow.Launcher.Plugin;
using Flow.Launcher.Storage;
using NUnit.Framework;

namespace Flow.Launcher.Test;

[TestFixture]
public class HistorySelectionTest
{
    [TestCase(true)]
    [TestCase(false)]
    public void Add_PresentationCopy_RefreshesStoredEntryWithoutDuplicatingIt(bool lastOpened)
    {
        var history = new Storage.History();
        var original = new LastOpenedHistoryResult
        {
            Title = "Original result",
            SubTitle = "Original subtitle",
            PluginID = "plugin",
            Query = "search text",
            OriginQuery = new Query { TrimmedQuery = "search text" },
            ExecutedDateTime = new DateTime(2020, 1, 1)
        };
        history.LastOpenedHistoryItems.Add(original);
        var copy = new LastOpenedHistoryResult { SourceHistoryItem = original, PluginID = string.Empty, UseProvenancePresentation = lastOpened };
        // Home-page result processing replaces the query with the empty home query.
        copy.OriginQuery = new Query { TrimmedQuery = string.Empty };
        var before = DateTime.Now;

        history.Add(copy);

        Assert.Multiple(() =>
        {
            Assert.That(history.LastOpenedHistoryItems, Has.Count.EqualTo(1));
            Assert.That(history.LastOpenedHistoryItems[0], Is.SameAs(original));
            Assert.That(original.ExecutedDateTime, Is.GreaterThanOrEqualTo(before));
            Assert.That(original.Query, Is.EqualTo("search text"));
            Assert.That(original.Title, Is.EqualTo("Original result"));
            Assert.That(original.SubTitle, Is.EqualTo("Original subtitle"));
            Assert.That(original.PluginID, Is.EqualTo("plugin"));
        });
    }

    [Test]
    public void Add_PresentationCopy_MovesStoredEntryToMostRecent()
    {
        var history = new Storage.History();
        var selected = new LastOpenedHistoryResult
        {
            Title = "Selected",
            Query = "selected",
            PluginID = "plugin",
            OriginQuery = new Query { TrimmedQuery = "selected" },
            ExecutedDateTime = new DateTime(2020, 1, 1)
        };
        var other = new LastOpenedHistoryResult
        {
            Title = "Other",
            Query = "other",
            PluginID = "plugin",
            OriginQuery = new Query { TrimmedQuery = "other" },
            ExecutedDateTime = new DateTime(2024, 1, 1)
        };
        history.LastOpenedHistoryItems.Add(selected);
        history.LastOpenedHistoryItems.Add(other);
        var copy = new LastOpenedHistoryResult
        {
            SourceHistoryItem = selected,
            PluginID = string.Empty,
            OriginQuery = new Query { TrimmedQuery = string.Empty }
        };
        var before = DateTime.Now;

        var changed = history.Add(copy);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(history.LastOpenedHistoryItems, Is.EqualTo(new[] { other, selected }));
            Assert.That(selected.ExecutedDateTime, Is.GreaterThanOrEqualTo(before));
            Assert.That(other.ExecutedDateTime, Is.EqualTo(new DateTime(2024, 1, 1)));
        });
    }
}

