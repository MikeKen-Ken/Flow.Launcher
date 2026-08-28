using System;
using System.IO;
using Flow.Launcher.Core.ImportExport;
using Flow.Launcher.Infrastructure;
using NUnit.Framework;

namespace Flow.Launcher.Test.ImportExport
{
    [TestFixture]
    public class ImportExportServiceTest
    {
        [Test]
        public void ExportToZip_ThenImport_StagesPendingZip()
        {
            var source = CreateDataTree();
            var destination = CreateDataTree();
            try
            {
                SeedSettings(source, "{\"ok\":true}");
                var service = new ImportExportService(source);
                var zipPath = Path.Combine(source.DataDirectory, "backup.zip");

                var export = service.ExportToZip(zipPath, includeSettings: true, includePlugins: false);
                Assert.That(export.Success, Is.True);
                Assert.That(File.Exists(zipPath), Is.True);

                var importService = new ImportExportService(destination);
                var import = importService.ImportFromZip(zipPath, applySettings: true, applyPlugins: false);

                Assert.That(import.Success, Is.True);
                Assert.That(import.RequiresRestart, Is.True);
                Assert.That(File.Exists(ImportExportPendingApply.GetPendingZipPath(destination.DataDirectory)), Is.True);
            }
            finally
            {
                Cleanup(source.DataDirectory);
                Cleanup(destination.DataDirectory);
            }
        }

        [Test]
        public void ExportToFolder_ThenImport_StagesPendingDirectory()
        {
            var source = CreateDataTree();
            var destination = CreateDataTree();
            var exportFolder = Path.Combine(Path.GetTempPath(), "FlowLauncherImportExportFolder-" + Guid.NewGuid().ToString("N"));
            try
            {
                SeedSettings(source, "{\"from\":\"folder\"}");
                var exportService = new ImportExportService(source);
                var export = exportService.ExportToFolder(exportFolder, includeSettings: true, includePlugins: false);
                Assert.That(export.Success, Is.True);
                Assert.That(File.Exists(Path.Combine(exportFolder, "manifest.json")), Is.True);

                var importService = new ImportExportService(destination);
                var import = importService.ImportFromFolder(exportFolder, applySettings: true, applyPlugins: false);

                Assert.That(import.Success, Is.True);
                Assert.That(Directory.Exists(ImportExportPendingApply.GetPendingDirectory(destination.DataDirectory)), Is.True);
            }
            finally
            {
                Cleanup(source.DataDirectory);
                Cleanup(destination.DataDirectory);
                Cleanup(exportFolder);
            }
        }

        [Test]
        public void PendingApply_AppliesStagedZip()
        {
            var source = CreateDataTree();
            var destination = CreateDataTree();
            try
            {
                SeedSettings(source, "{\"applied\":true}");
                var zipPath = Path.Combine(source.DataDirectory, "pending-source.zip");
                ImportExportPackage.CreateZip(zipPath, source, includeSettings: true, includePlugins: false);

                ImportExportPendingApply.StageZip(zipPath, destination.DataDirectory, applySettings: true, applyPlugins: false);
                var applied = ImportExportPendingApply.ApplyIfNeeded(destination.DataDirectory);

                Assert.That(applied, Is.True);
                var appliedJson = File.ReadAllText(Path.Combine(destination.SettingsDirectory, "Settings.json"));
                Assert.That(appliedJson, Does.Contain("applied"));
                Assert.That(File.Exists(ImportExportPendingApply.GetPendingZipPath(destination.DataDirectory)), Is.False);
            }
            finally
            {
                Cleanup(source.DataDirectory);
                Cleanup(destination.DataDirectory);
            }
        }

        [Test]
        public void PendingApply_AppliesStagedDirectory()
        {
            var source = CreateDataTree();
            var destination = CreateDataTree();
            var packageDirectory = Path.Combine(Path.GetTempPath(), "FlowLauncherImportExportPendingDir-" + Guid.NewGuid().ToString("N"));
            try
            {
                SeedSettings(source, "{\"folderApplied\":true}");
                ImportExportPackage.WriteToDirectory(packageDirectory, source, includeSettings: true, includePlugins: false);

                ImportExportPendingApply.StageDirectory(packageDirectory, destination.DataDirectory, applySettings: true, applyPlugins: false);
                var applied = ImportExportPendingApply.ApplyIfNeeded(destination.DataDirectory);

                Assert.That(applied, Is.True);
                Assert.That(File.ReadAllText(Path.Combine(destination.SettingsDirectory, "Settings.json")),
                    Does.Contain("folderApplied"));
                Assert.That(Directory.Exists(ImportExportPendingApply.GetPendingDirectory(destination.DataDirectory)), Is.False);
            }
            finally
            {
                Cleanup(source.DataDirectory);
                Cleanup(destination.DataDirectory);
                Cleanup(packageDirectory);
            }
        }

        [Test]
        public void Export_WhenNothingSelected_Fails()
        {
            var data = CreateDataTree();
            try
            {
                var service = new ImportExportService(data);
                var result = service.ExportToZip(
                    Path.Combine(data.DataDirectory, "backup.zip"),
                    includeSettings: false,
                    includePlugins: false);

                Assert.That(result.Success, Is.False);
                Assert.That(result.ErrorMessage, Is.Not.Empty);
            }
            finally
            {
                Cleanup(data.DataDirectory);
            }
        }

        [Test]
        public void ExportToFolder_WhenDestinationIsDataDirectory_Fails()
        {
            var data = CreateDataTree();
            try
            {
                SeedSettings(data, "{\"ok\":true}");
                var service = new ImportExportService(data);
                var result = service.ExportToFolder(data.DataDirectory, includeSettings: true, includePlugins: false);

                Assert.That(result.Success, Is.False);
                Assert.That(result.ErrorMessage, Is.Not.Empty);
            }
            finally
            {
                Cleanup(data.DataDirectory);
            }
        }

        [Test]
        public void ImportFromZip_WhenFileIsNotAPackage_Fails()
        {
            var data = CreateDataTree();
            try
            {
                var zipPath = Path.Combine(data.DataDirectory, "not-a-package.zip");
                Directory.CreateDirectory(data.DataDirectory);
                File.WriteAllBytes(zipPath, new byte[] { 0x50, 0x4B, 0x05, 0x06, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                var service = new ImportExportService(data);
                var result = service.ImportFromZip(zipPath, applySettings: true, applyPlugins: true);

                Assert.That(result.Success, Is.False);
            }
            finally
            {
                Cleanup(data.DataDirectory);
            }
        }

        [Test]
        public void ImportFromFolder_WhenPluginsMissingAndOnlyPluginsRequested_Fails()
        {
            var source = CreateDataTree();
            var destination = CreateDataTree();
            var exportFolder = Path.Combine(Path.GetTempPath(), "FlowLauncherImportExportSettingsOnly-" + Guid.NewGuid().ToString("N"));
            try
            {
                SeedSettings(source, "{\"ok\":true}");
                var exportService = new ImportExportService(source);
                Assert.That(exportService.ExportToFolder(exportFolder, includeSettings: true, includePlugins: false).Success, Is.True);

                var importService = new ImportExportService(destination);
                var result = importService.ImportFromFolder(exportFolder, applySettings: false, applyPlugins: true);

                Assert.That(result.Success, Is.False);
            }
            finally
            {
                Cleanup(source.DataDirectory);
                Cleanup(destination.DataDirectory);
                Cleanup(exportFolder);
            }
        }

        private static void SeedSettings(ImportExportPaths paths, string json)
        {
            Directory.CreateDirectory(paths.SettingsDirectory);
            File.WriteAllText(Path.Combine(paths.SettingsDirectory, "Settings.json"), json);
        }

        private static ImportExportPaths CreateDataTree()
        {
            var root = Path.Combine(Path.GetTempPath(), "FlowLauncherImportExportService-" + Guid.NewGuid().ToString("N"));
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
