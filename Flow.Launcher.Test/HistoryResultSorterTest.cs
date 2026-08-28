using System;
using System.Linq;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Storage;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    [SetCulture("en-US")]
    [SetUICulture("en-US")]
    public class HistoryResultSorterTest
    {
        [Test]
        public void Prepare_NewestFirst_OrdersByExecutedTimeDescending()
        {
            var items = new[]
            {
                Item("Charlie", new DateTime(2024, 1, 1)),
                Item("Alpha", new DateTime(2024, 3, 1)),
                Item("Bravo", new DateTime(2024, 2, 1))
            };

            var result = HistoryResultSorter.Prepare(items, HistoryStyle.Query, HistorySortOrder.NewestFirst).ToList();

            ClassicAssert.AreEqual(new[] { "Alpha", "Bravo", "Charlie" }, result.Select(x => x.Title).ToArray());
        }

        [Test]
        public void Prepare_OldestFirst_OrdersByExecutedTimeAscending()
        {
            var items = new[]
            {
                Item("Charlie", new DateTime(2024, 1, 1)),
                Item("Alpha", new DateTime(2024, 3, 1)),
                Item("Bravo", new DateTime(2024, 2, 1))
            };

            var result = HistoryResultSorter.Prepare(items, HistoryStyle.Query, HistorySortOrder.OldestFirst).ToList();

            ClassicAssert.AreEqual(new[] { "Charlie", "Bravo", "Alpha" }, result.Select(x => x.Title).ToArray());
        }

        [Test]
        public void Prepare_TitleAscending_OrdersAlphabetically()
        {
            var items = new[]
            {
                Item("Charlie", new DateTime(2024, 3, 1)),
                Item("alpha", new DateTime(2024, 1, 1)),
                Item("Bravo", new DateTime(2024, 2, 1))
            };

            var result = HistoryResultSorter.Prepare(items, HistoryStyle.Query, HistorySortOrder.TitleAscending).ToList();

            ClassicAssert.AreEqual(new[] { "alpha", "Bravo", "Charlie" }, result.Select(x => x.Title).ToArray());
        }

        [Test]
        public void Prepare_TitleDescending_OrdersReverseAlphabetically()
        {
            var items = new[]
            {
                Item("Charlie", new DateTime(2024, 3, 1)),
                Item("alpha", new DateTime(2024, 1, 1)),
                Item("Bravo", new DateTime(2024, 2, 1))
            };

            var result = HistoryResultSorter.Prepare(items, HistoryStyle.Query, HistorySortOrder.TitleDescending).ToList();

            ClassicAssert.AreEqual(new[] { "Charlie", "Bravo", "alpha" }, result.Select(x => x.Title).ToArray());
        }

        [Test]
        public void Prepare_LastOpenedStyle_KeepsMostRecentUniqueResultBeforeSorting()
        {
            var items = new[]
            {
                Item("Notes", new DateTime(2024, 1, 1), subtitle: "app"),
                Item("Notes", new DateTime(2024, 3, 1), subtitle: "app"),
                Item("Calc", new DateTime(2024, 2, 1), subtitle: "app")
            };

            var result = HistoryResultSorter.Prepare(items, HistoryStyle.LastOpened, HistorySortOrder.OldestFirst).ToList();

            ClassicAssert.AreEqual(2, result.Count);
            ClassicAssert.AreEqual("Calc", result[0].Title);
            ClassicAssert.AreEqual("Notes", result[1].Title);
            ClassicAssert.AreEqual(new DateTime(2024, 3, 1), result[1].ExecutedDateTime);
        }

        [Test]
        public void Prepare_MaxResult_LimitsAfterSorting()
        {
            var items = new[]
            {
                Item("Charlie", new DateTime(2024, 1, 1)),
                Item("Alpha", new DateTime(2024, 3, 1)),
                Item("Bravo", new DateTime(2024, 2, 1))
            };

            var result = HistoryResultSorter.Prepare(items, HistoryStyle.Query, HistorySortOrder.NewestFirst, maxResult: 2).ToList();

            ClassicAssert.AreEqual(new[] { "Alpha", "Bravo" }, result.Select(x => x.Title).ToArray());
        }

        [Test]
        public void Prepare_QueryStyle_KeepsDuplicateResults()
        {
            var items = new[]
            {
                Item("Notes", new DateTime(2024, 1, 1)),
                Item("Notes", new DateTime(2024, 3, 1))
            };

            var result = HistoryResultSorter.Prepare(items, HistoryStyle.Query, HistorySortOrder.NewestFirst).ToList();

            ClassicAssert.AreEqual(2, result.Count);
        }

        private static LastOpenedHistoryResult Item(
            string title,
            DateTime executed,
            string subtitle = "",
            string pluginId = "plugin",
            string recordKey = "key")
        {
            return new LastOpenedHistoryResult
            {
                Title = title,
                SubTitle = subtitle,
                PluginID = pluginId,
                RecordKey = recordKey,
                ExecutedDateTime = executed
            };
        }
    }
}
