using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Core.WebDavSync;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using NUnit.Framework;

namespace Flow.Launcher.Test.WebDavSync
{
    [TestFixture]
    public class WebDavSyncServiceTest
    {
        [Test]
        public async Task Execute_Upload_StoresZipAndManifestOnTransportAsync()
        {
            var data = CreateDataTree();
            try
            {
                Directory.CreateDirectory(data.SettingsDirectory);
                File.WriteAllText(Path.Combine(data.SettingsDirectory, "Settings.json"), "{\"ok\":true}");

                var transport = new MemoryWebDavTransport();
                var service = new WebDavSyncService(transport, data);
                var settings = CreateSettings();

                var result = await service.ExecuteAsync(WebDavSyncOperation.Upload, settings);

                Assert.That(result.Success, Is.True);
                Assert.That(result.ActionTaken, Is.EqualTo(WebDavSyncActionTaken.Uploaded));
                Assert.That(result.RequiresRestart, Is.False);
                Assert.That(transport.Files.ContainsKey(WebDavSyncConstants.RemoteZipFileName), Is.True);
                Assert.That(transport.Files.ContainsKey(WebDavSyncConstants.RemoteManifestFileName), Is.True);
                Assert.That(settings.LastSuccessfulSyncUtc, Is.Not.Null);
            }
            finally
            {
                Cleanup(data.DataDirectory);
            }
        }

        [Test]
        public async Task Execute_Download_StagesPendingZipAsync()
        {
            var source = CreateDataTree();
            var destination = CreateDataTree();
            try
            {
                Directory.CreateDirectory(source.SettingsDirectory);
                File.WriteAllText(Path.Combine(source.SettingsDirectory, "Settings.json"), "{\"from\":\"remote\"}");
                var zipPath = Path.Combine(source.DataDirectory, "remote.zip");
                WebDavSyncArchive.CreateZip(zipPath, source, includeSettings: true, includePlugins: false);

                var transport = new MemoryWebDavTransport();
                transport.Files[WebDavSyncConstants.RemoteZipFileName] = File.ReadAllBytes(zipPath);

                var service = new WebDavSyncService(transport, destination);
                var result = await service.ExecuteAsync(WebDavSyncOperation.Download, CreateSettings());

                Assert.That(result.Success, Is.True);
                Assert.That(result.ActionTaken, Is.EqualTo(WebDavSyncActionTaken.Downloaded));
                Assert.That(result.RequiresRestart, Is.True);
                Assert.That(File.Exists(WebDavPendingApply.GetPendingZipPath(destination.DataDirectory)), Is.True);
            }
            finally
            {
                Cleanup(source.DataDirectory);
                Cleanup(destination.DataDirectory);
            }
        }

        [Test]
        public async Task Execute_Sync_WhenRemoteIsMissing_UploadsAsync()
        {
            var data = CreateDataTree();
            try
            {
                Directory.CreateDirectory(data.SettingsDirectory);
                File.WriteAllText(Path.Combine(data.SettingsDirectory, "Settings.json"), "{\"ok\":true}");

                var transport = new MemoryWebDavTransport();
                var service = new WebDavSyncService(transport, data);
                var result = await service.ExecuteAsync(WebDavSyncOperation.Sync, CreateSettings());

                Assert.That(result.Success, Is.True);
                Assert.That(result.ActionTaken, Is.EqualTo(WebDavSyncActionTaken.Uploaded));
            }
            finally
            {
                Cleanup(data.DataDirectory);
            }
        }

        [Test]
        public async Task Execute_WhenNothingSelected_FailsAsync()
        {
            var data = CreateDataTree();
            try
            {
                var settings = CreateSettings();
                settings.SyncSettings = false;
                settings.SyncPlugins = false;
                var service = new WebDavSyncService(new MemoryWebDavTransport(), data);

                var result = await service.ExecuteAsync(WebDavSyncOperation.Upload, settings);

                Assert.That(result.Success, Is.False);
                Assert.That(result.ErrorMessage, Is.Not.Empty);
            }
            finally
            {
                Cleanup(data.DataDirectory);
            }
        }

        [Test]
        public async Task PendingApply_AppliesStagedZipAsync()
        {
            var source = CreateDataTree();
            var destination = CreateDataTree();
            try
            {
                Directory.CreateDirectory(source.SettingsDirectory);
                File.WriteAllText(Path.Combine(source.SettingsDirectory, "Settings.json"), "{\"applied\":true}");
                var zipPath = Path.Combine(source.DataDirectory, "pending-source.zip");
                WebDavSyncArchive.CreateZip(zipPath, source, includeSettings: true, includePlugins: false);

                WebDavPendingApply.StageDownloadedZip(zipPath, destination.DataDirectory);
                var applied = WebDavPendingApply.ApplyIfNeeded(destination.DataDirectory);

                Assert.That(applied, Is.True);
                var appliedJson = File.ReadAllText(Path.Combine(destination.SettingsDirectory, "Settings.json"));
                Assert.That(appliedJson, Does.Contain("applied"));
                Assert.That(appliedJson, Does.Contain("LastSuccessfulSyncUtc"));
                Assert.That(File.Exists(WebDavPendingApply.GetPendingZipPath(destination.DataDirectory)), Is.False);
            }
            finally
            {
                Cleanup(source.DataDirectory);
                Cleanup(destination.DataDirectory);
            }
        }

        [Test]
        public void CombineUrl_AppendsFileNameToDirectory()
        {
            var combined = WebDavTransport.CombineUrl("https://dav.example.com/FlowLauncher/", "flow-launcher-sync.zip");
            Assert.That(combined, Is.EqualTo("https://dav.example.com/FlowLauncher/flow-launcher-sync.zip"));
        }

        private static WebDavSyncSettings CreateSettings() => new()
        {
            Url = "https://dav.example.com/FlowLauncher",
            UserName = "user",
            Password = "secret",
            SyncSettings = true,
            SyncPlugins = true
        };

        private static WebDavSyncPaths CreateDataTree()
        {
            var root = Path.Combine(Path.GetTempPath(), "FlowLauncherWebDavService-" + Guid.NewGuid().ToString("N"));
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

        private sealed class MemoryWebDavTransport : IWebDavTransport
        {
            public Dictionary<string, byte[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

            public Task TestConnectionAsync(WebDavConnection connection, CancellationToken token = default) =>
                Task.CompletedTask;

            public Task EnsureDirectoryAsync(WebDavConnection connection, CancellationToken token = default) =>
                Task.CompletedTask;

            public Task<WebDavRemoteFileInfo> GetFileInfoAsync(
                WebDavConnection connection,
                string fileName,
                CancellationToken token = default)
            {
                if (!Files.TryGetValue(fileName, out var bytes))
                {
                    return Task.FromResult(WebDavRemoteFileInfo.Missing);
                }

                return Task.FromResult(new WebDavRemoteFileInfo
                {
                    Exists = true,
                    Length = bytes.Length,
                    LastModifiedUtc = DateTime.UtcNow.AddMinutes(-5)
                });
            }

            public async Task UploadFileAsync(
                WebDavConnection connection,
                string fileName,
                Stream content,
                CancellationToken token = default)
            {
                using var buffer = new MemoryStream();
                await content.CopyToAsync(buffer, token);
                Files[fileName] = buffer.ToArray();
            }

            public async Task DownloadFileAsync(
                WebDavConnection connection,
                string fileName,
                Stream destination,
                CancellationToken token = default)
            {
                if (!Files.TryGetValue(fileName, out var bytes))
                {
                    throw new FileNotFoundException(fileName);
                }

                await destination.WriteAsync(bytes, token);
            }
        }
    }
}
