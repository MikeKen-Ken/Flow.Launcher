using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using Flow.Launcher.Core.WebDavSync;
using Flow.Launcher.Infrastructure;
using NUnit.Framework;

namespace Flow.Launcher.Test.WebDavSync
{
    [TestFixture]
    public class WebDavSyncArchiveTest
    {
        [Test]
        public void CreateZipAndApply_RoundTripsSettingsAndPlugins()
        {
            var source = CreateDataTree("source");
            var destination = CreateDataTree("destination");

            try
            {
                Directory.CreateDirectory(source.SettingsDirectory);
                Directory.CreateDirectory(source.PluginsDirectory);
                Directory.CreateDirectory(source.ThemesDirectory);
                File.WriteAllText(Path.Combine(source.SettingsDirectory, "Settings.json"), "{\"theme\":\"dark\"}");
                Directory.CreateDirectory(Path.Combine(source.PluginsDirectory, "SamplePlugin"));
                File.WriteAllText(Path.Combine(source.PluginsDirectory, "SamplePlugin", "plugin.json"), "{\"Name\":\"Sample\"}");
                Directory.CreateDirectory(source.ThemesDirectory);
                File.WriteAllText(Path.Combine(source.ThemesDirectory, "Custom.xaml"), "<Theme />");

                var zipPath = Path.Combine(source.DataDirectory, "pack.zip");
                var manifest = WebDavSyncArchive.CreateZip(zipPath, source, includeSettings: true, includePlugins: true);

                Assert.That(File.Exists(zipPath), Is.True);
                Assert.That(manifest.IncludesSettings, Is.True);
                Assert.That(manifest.IncludesPlugins, Is.True);

                WebDavSyncArchive.ApplyFromZip(zipPath, destination, applySettings: true, applyPlugins: true);

                var appliedSettings = JsonNode.Parse(
                    File.ReadAllText(Path.Combine(destination.SettingsDirectory, "Settings.json")))!.AsObject();
                Assert.That(appliedSettings["theme"]!.GetValue<string>(), Is.EqualTo("dark"));
                Assert.That(File.ReadAllText(Path.Combine(destination.PluginsDirectory, "SamplePlugin", "plugin.json")),
                    Is.EqualTo("{\"Name\":\"Sample\"}"));
                Assert.That(File.ReadAllText(Path.Combine(destination.ThemesDirectory, "Custom.xaml")),
                    Is.EqualTo("<Theme />"));
            }
            finally
            {
                Cleanup(source.DataDirectory);
                Cleanup(destination.DataDirectory);
            }
        }

        [Test]
        public void GetLocalMaxWriteUtc_UsesNewestIncludedFile()
        {
            var paths = CreateDataTree("max-write");
            try
            {
                Directory.CreateDirectory(paths.SettingsDirectory);
                var older = Path.Combine(paths.SettingsDirectory, "old.json");
                var newer = Path.Combine(paths.SettingsDirectory, "new.json");
                File.WriteAllText(older, "1");
                File.WriteAllText(newer, "2");
                File.SetLastWriteTimeUtc(older, new System.DateTime(2024, 1, 1, 0, 0, 0, System.DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(newer, new System.DateTime(2026, 5, 1, 12, 0, 0, System.DateTimeKind.Utc));

                var max = WebDavSyncArchive.GetLocalMaxWriteUtc(paths, includeSettings: true, includePlugins: false);

                Assert.That(max.HasValue, Is.True);
                Assert.That(max.Value, Is.EqualTo(new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc)).Within(TimeSpan.FromSeconds(2)));
            }
            finally
            {
                Cleanup(paths.DataDirectory);
            }
        }

        [Test]
        public void CreateZipAndApply_ExcludesAndPreservesLocalWebDavSettings()
        {
            var source = CreateDataTree("webdav-source");
            var destination = CreateDataTree("webdav-destination");
            try
            {
                Directory.CreateDirectory(source.SettingsDirectory);
                Directory.CreateDirectory(destination.SettingsDirectory);
                File.WriteAllText(Path.Combine(source.SettingsDirectory, "Settings.json"),
                    "{\"theme\":\"dark\",\"WebDavSync\":{\"Url\":\"https://source.example\",\"Password\":\"source-secret\"}}");
                File.WriteAllText(Path.Combine(destination.SettingsDirectory, "Settings.json"),
                    "{\"theme\":\"light\",\"WebDavSync\":{\"Url\":\"https://destination.example\",\"Password\":\"destination-secret\"}}");
                var zipPath = Path.Combine(source.DataDirectory, "pack.zip");

                WebDavSyncArchive.CreateZip(zipPath, source, includeSettings: true, includePlugins: false);

                using (var archive = ZipFile.OpenRead(zipPath))
                using (var reader = new StreamReader(archive.GetEntry("Settings/Settings.json")!.Open()))
                {
                    var archivedSettings = JsonNode.Parse(reader.ReadToEnd())!.AsObject();
                    Assert.That(archivedSettings.ContainsKey("WebDavSync"), Is.False);
                }

                WebDavSyncArchive.ApplyFromZip(zipPath, destination, applySettings: true, applyPlugins: false);

                var appliedSettings = JsonNode.Parse(File.ReadAllText(Path.Combine(destination.SettingsDirectory, "Settings.json")))!.AsObject();
                Assert.That(appliedSettings["theme"]!.GetValue<string>(), Is.EqualTo("dark"));
                Assert.That(appliedSettings["WebDavSync"]!["Url"]!.GetValue<string>(),
                    Is.EqualTo("https://destination.example"));
                Assert.That(appliedSettings["WebDavSync"]!["Password"]!.GetValue<string>(),
                    Is.EqualTo("destination-secret"));
            }
            finally
            {
                Cleanup(source.DataDirectory);
                Cleanup(destination.DataDirectory);
            }
        }

        private static WebDavSyncPaths CreateDataTree(string suffix)
        {
            var root = Path.Combine(Path.GetTempPath(), "FlowLauncherWebDavArchive-" + suffix + "-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new WebDavSyncPaths
            {
                DataDirectory = root,
                SettingsDirectory = Path.Combine(root, Constant.Settings),
                PluginsDirectory = Path.Combine(root, Constant.Plugins),
                ThemesDirectory = Path.Combine(root, Constant.Themes)
            };
        }

        private static void Cleanup(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
