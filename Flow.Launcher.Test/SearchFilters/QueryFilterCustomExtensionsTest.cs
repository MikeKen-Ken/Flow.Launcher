using System;
using System.IO;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.SearchFilters;
using NUnit.Framework;

namespace Flow.Launcher.Test.SearchFilters;

public class QueryFilterCustomExtensionsTest
{
    [Test]
    public void CustomExtensions_SurviveReloadAndOnlyDisappearOnExplicitRemoval()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FlowExtensionTest-" + Guid.NewGuid());
        var path = Path.Combine(directory, "extensions.json");
        var storage = new JsonStorage<CustomExtensionSettings>(path);
        try
        {
            var saved = new QueryFilterCustomExtensions(storage);
            saved.Add(".HEIC");
            saved.Add("heic");
            saved.Add("appref-ms");
            saved.Add("log");
            saved.Add("png");
            saved.Add("log;exe");

            var reloaded = new QueryFilterCustomExtensions(new JsonStorage<CustomExtensionSettings>(path));
            Assert.That(reloaded.Values, Is.EquivalentTo(new[] { "heic", "appref-ms", "log" }));

            // Clearing a query must not erase saved choices.
            QueryFilterSyntax.Apply("notes ext:heic", QueryFilterId.Extension, "", QueryFilterApplyMode.Clear);
            Assert.That(reloaded.Values, Does.Contain("heic"));
            reloaded.Remove("HEIC");
            Assert.That(new QueryFilterCustomExtensions(new JsonStorage<CustomExtensionSettings>(path)).Values,
                Is.EquivalentTo(new[] { "appref-ms", "log" }));
        }
        finally
        {
            storage.Delete();
            Directory.Delete(directory);
        }
    }
}
