using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using Flow.Launcher.Core.ImportExport;
using Flow.Launcher.Infrastructure;
using NUnit.Framework;

namespace Flow.Launcher.Test.ImportExport
{
    [TestFixture]
    public class ImportExportPackageTest
    {
        [Test]
        public void CreateZipAndApply_RoundTripsSettingsAndPlugins()
        {
            var source = CreateDataTree("source");
            var destination = CreateDataTree("destination");

            try
            {
                SeedSampleData(source, theme: "dark");

                var zipPath = Path.Combine(source.DataDirectory, "pack.zip");
                var manifest = ImportExportPackage.CreateZip(zipPath, source, includeSettings: true, includePlugins: true);

                Assert.That(File.Exists(zipPath), Is.True);
                Assert.That(manifest.IncludesSettings, Is.True);
                Assert.That(manifest.IncludesPlugins, Is.True);

                ImportExportPackage.ApplyFromZip(zipPath, destination, applySettings: true, applyPlugins: true);

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
        public void WriteToDirectoryAndApply_RoundTripsSettingsAndPlugins()
        {
            var source = CreateDataTree("folder-source");
            var destination = CreateDataTree("folder-destination");
            var packageDirectory = Path.Combine(Path.GetTempPath(), "FlowLauncherImportExportPackage-" + System.Guid.NewGuid().ToString("N"));

            try
            {
                SeedSampleData(source, theme: "light");
                var manifest = ImportExportPackage.WriteToDirectory(
                    packageDirectory, source, includeSettings: true, includePlugins: true);

                Assert.That(File.Exists(Path.Combine(packageDirectory, "manifest.json")), Is.True);
                Assert.That(manifest.IncludesSettings, Is.True);
                Assert.That(Directory.Exists(Path.Combine(packageDirectory, Constant.Settings)), Is.True);

                ImportExportPackage.ApplyFromDirectory(packageDirectory, destination, applySettings: true, applyPlugins: true);

                var appliedSettings = JsonNode.Parse(
                    File.ReadAllText(Path.Combine(destination.SettingsDirectory, "Settings.json")))!.AsObject();
                Assert.That(appliedSettings["theme"]!.GetValue<string>(), Is.EqualTo("light"));
                Assert.That(File.Exists(Path.Combine(destination.PluginsDirectory, "SamplePlugin", "plugin.json")), Is.True);
            }
            finally
            {
                Cleanup(source.DataDirectory);
                Cleanup(destination.DataDirectory);
                Cleanup(packageDirectory);
            }
        }

        [Test]
        public void ApplyFromZip_CanSkipPlugins()
        {
            var source = CreateDataTree("skip-source");
            var destination = CreateDataTree("skip-destination");

            try
            {
                SeedSampleData(source, theme: "dark");
                Directory.CreateDirectory(destination.PluginsDirectory);
                File.WriteAllText(Path.Combine(destination.PluginsDirectory, "keep.json"), "{\"keep\":true}");

                var zipPath = Path.Combine(source.DataDirectory, "pack.zip");
                ImportExportPackage.CreateZip(zipPath, source, includeSettings: true, includePlugins: true);
                ImportExportPackage.ApplyFromZip(zipPath, destination, applySettings: true, applyPlugins: false);

                Assert.That(File.ReadAllText(Path.Combine(destination.SettingsDirectory, "Settings.json")),
                    Does.Contain("dark"));
                Assert.That(File.Exists(Path.Combine(destination.PluginsDirectory, "keep.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(destination.PluginsDirectory, "SamplePlugin", "plugin.json")), Is.False);
            }
            finally
            {
                Cleanup(source.DataDirectory);
                Cleanup(destination.DataDirectory);
            }
        }

        [Test]
        public void IsPackageZip_AcceptsZipWithoutManifestWhenSettingsFolderExists()
        {
            var source = CreateDataTree("no-manifest");
            try
            {
                SeedSampleData(source, theme: "dark");
                var zipPath = Path.Combine(source.DataDirectory, "legacy.zip");
                var staging = Path.Combine(source.DataDirectory, "staging");
                Directory.CreateDirectory(Path.Combine(staging, Constant.Settings));
                File.Copy(
                    Path.Combine(source.SettingsDirectory, "Settings.json"),
                    Path.Combine(staging, Constant.Settings, "Settings.json"));
                ZipFile.CreateFromDirectory(staging, zipPath, CompressionLevel.Fastest, includeBaseDirectory: false);

                Assert.That(ImportExportPackage.IsPackageZip(zipPath), Is.True);
                var manifest = ImportExportPackage.ReadManifestFromZip(zipPath);
                Assert.That(manifest.IncludesSettings, Is.True);
                Assert.That(manifest.IncludesPlugins, Is.False);
            }
            finally
            {
                Cleanup(source.DataDirectory);
            }
        }

        [Test]
        public void OverlapsDataPaths_DetectsFlowLauncherDataFolder()
        {
            var paths = CreateDataTree("overlap");
            try
            {
                Assert.That(ImportExportPackage.OverlapsDataPaths(paths.DataDirectory, paths), Is.True);
                Assert.That(ImportExportPackage.OverlapsDataPaths(paths.SettingsDirectory, paths), Is.True);
                Assert.That(ImportExportPackage.OverlapsDataPaths(Path.Combine(paths.DataDirectory, "Logs"), paths), Is.True);
                Assert.That(ImportExportPackage.OverlapsDataPaths(Path.Combine(Path.GetTempPath(), "unrelated-export"), paths), Is.False);
            }
            finally
            {
                Cleanup(paths.DataDirectory);
            }
        }

        private static void SeedSampleData(ImportExportPaths paths, string theme)
        {
            Directory.CreateDirectory(paths.SettingsDirectory);
            Directory.CreateDirectory(paths.PluginsDirectory);
            Directory.CreateDirectory(paths.ThemesDirectory);
            File.WriteAllText(Path.Combine(paths.SettingsDirectory, "Settings.json"), $"{{\"theme\":\"{theme}\"}}");
            Directory.CreateDirectory(Path.Combine(paths.PluginsDirectory, "SamplePlugin"));
            File.WriteAllText(Path.Combine(paths.PluginsDirectory, "SamplePlugin", "plugin.json"), "{\"Name\":\"Sample\"}");
            File.WriteAllText(Path.Combine(paths.ThemesDirectory, "Custom.xaml"), "<Theme />");
        }

        private static ImportExportPaths CreateDataTree(string suffix)
        {
            var root = Path.Combine(Path.GetTempPath(), "FlowLauncherImportExportPackage-" + suffix + "-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new ImportExportPaths
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
