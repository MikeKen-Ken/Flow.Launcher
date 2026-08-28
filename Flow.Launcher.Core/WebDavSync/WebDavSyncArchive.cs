using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Core.WebDavSync;

public static class WebDavSyncArchive
{
    private static readonly string ClassName = nameof(WebDavSyncArchive);
    private const string SettingsFileName = "Settings.json";
    private const string WebDavSyncPropertyName = "WebDavSync";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static WebDavSyncManifest CreateZip(
        string zipPath,
        WebDavSyncPaths paths,
        bool includeSettings,
        bool includePlugins)
    {
        var staging = Path.Combine(Path.GetTempPath(), "FlowLauncherWebDavPack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            var manifest = new WebDavSyncManifest
            {
                Version = 1,
                ExportedAtUtc = DateTime.UtcNow,
                DeviceName = Environment.MachineName,
                IncludesSettings = includeSettings,
                IncludesPlugins = includePlugins
            };

            if (includeSettings)
            {
                CopyDirectory(paths.SettingsDirectory, Path.Combine(staging, Constant.Settings));
                CopyDirectory(paths.ThemesDirectory, Path.Combine(staging, Constant.Themes));
                RemoveWebDavSyncSettings(Path.Combine(staging, Constant.Settings, SettingsFileName));
            }

            if (includePlugins)
            {
                CopyDirectory(paths.PluginsDirectory, Path.Combine(staging, Constant.Plugins));
            }

            File.WriteAllText(
                Path.Combine(staging, WebDavSyncConstants.ManifestEntryName),
                JsonSerializer.Serialize(manifest, JsonOptions));

            var zipDirectory = Path.GetDirectoryName(zipPath);
            if (!string.IsNullOrEmpty(zipDirectory))
            {
                Directory.CreateDirectory(zipDirectory);
            }

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            ZipFile.CreateFromDirectory(staging, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            return manifest;
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    public static WebDavSyncManifest ReadManifest(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.GetEntry(WebDavSyncConstants.ManifestEntryName);
        if (entry == null)
        {
            return new WebDavSyncManifest
            {
                Version = 1,
                ExportedAtUtc = File.GetLastWriteTimeUtc(zipPath)
            };
        }

        using var stream = entry.Open();
        return JsonSerializer.Deserialize<WebDavSyncManifest>(stream, JsonOptions) ?? new WebDavSyncManifest();
    }

    public static DateTime? GetLocalMaxWriteUtc(WebDavSyncPaths paths, bool includeSettings, bool includePlugins)
    {
        DateTime? max = null;

        if (includeSettings)
        {
            max = MaxWrite(max, GetDirectoryMaxWriteUtc(paths.SettingsDirectory));
            max = MaxWrite(max, GetDirectoryMaxWriteUtc(paths.ThemesDirectory));
        }

        if (includePlugins)
        {
            max = MaxWrite(max, GetDirectoryMaxWriteUtc(paths.PluginsDirectory));
        }

        return max;
    }

    public static WebDavSyncManifest ExtractToDirectory(string zipPath, string destinationDirectory)
    {
        if (Directory.Exists(destinationDirectory))
        {
            TryDeleteDirectory(destinationDirectory);
        }

        Directory.CreateDirectory(destinationDirectory);
        ZipFile.ExtractToDirectory(zipPath, destinationDirectory);
        var manifestPath = Path.Combine(destinationDirectory, WebDavSyncConstants.ManifestEntryName);
        if (!File.Exists(manifestPath))
        {
            return new WebDavSyncManifest
            {
                Version = 1,
                ExportedAtUtc = File.GetLastWriteTimeUtc(zipPath),
                IncludesSettings = Directory.Exists(Path.Combine(destinationDirectory, Constant.Settings)),
                IncludesPlugins = Directory.Exists(Path.Combine(destinationDirectory, Constant.Plugins))
            };
        }

        return JsonSerializer.Deserialize<WebDavSyncManifest>(File.ReadAllText(manifestPath), JsonOptions)
               ?? new WebDavSyncManifest();
    }

    public static void ApplyExtracted(
        string extractedDirectory,
        WebDavSyncPaths paths,
        bool applySettings,
        bool applyPlugins)
    {
        if (applySettings)
        {
            var localWebDavSyncSettings = ReadWebDavSyncSettings(
                Path.Combine(paths.SettingsDirectory, SettingsFileName));
            CopyDirectory(Path.Combine(extractedDirectory, Constant.Settings), paths.SettingsDirectory, overwrite: true);
            CopyDirectory(Path.Combine(extractedDirectory, Constant.Themes), paths.ThemesDirectory, overwrite: true);
            RestoreWebDavSyncSettings(
                Path.Combine(paths.SettingsDirectory, SettingsFileName),
                localWebDavSyncSettings);
        }

        if (applyPlugins)
        {
            CopyDirectory(Path.Combine(extractedDirectory, Constant.Plugins), paths.PluginsDirectory, overwrite: true);
        }
    }

    public static void ApplyFromZip(
        string zipPath,
        WebDavSyncPaths paths,
        bool applySettings,
        bool applyPlugins)
    {
        var staging = Path.Combine(Path.GetTempPath(), "FlowLauncherWebDavApply-" + Guid.NewGuid().ToString("N"));
        try
        {
            var manifest = ExtractToDirectory(zipPath, staging);
            ApplyExtracted(
                staging,
                paths,
                applySettings && (manifest.IncludesSettings || Directory.Exists(Path.Combine(staging, Constant.Settings))),
                applyPlugins && (manifest.IncludesPlugins || Directory.Exists(Path.Combine(staging, Constant.Plugins))));
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    private static DateTime? GetDirectoryMaxWriteUtc(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        DateTime? max = null;
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            try
            {
                var write = File.GetLastWriteTimeUtc(file);
                max = MaxWrite(max, write);
            }
            catch (Exception e)
            {
                Log.Info(ClassName, $"Skipped last-write time for {file}: {e.Message}");
            }
        }

        return max;
    }

    private static DateTime? MaxWrite(DateTime? current, DateTime? candidate)
    {
        if (!candidate.HasValue)
        {
            return current;
        }

        if (!current.HasValue || candidate.Value > current.Value)
        {
            return candidate;
        }

        return current;
    }

    private static void CopyDirectory(string source, string destination, bool overwrite = false)
    {
        if (!Directory.Exists(source))
        {
            return;
        }

        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            var targetDirectory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            CopyFile(file, target, overwrite);
        }
    }

    private static void CopyFile(string source, string destination, bool overwrite)
    {
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var output = new FileStream(
            destination,
            overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        input.CopyTo(output);
    }

    private static void RemoveWebDavSyncSettings(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return;
        }

        var root = JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject;
        if (root == null)
        {
            return;
        }

        root.Remove(WebDavSyncPropertyName);
        File.WriteAllText(settingsPath, root.ToJsonString(JsonOptions));
    }

    private static JsonNode ReadWebDavSyncSettings(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return null;
        }

        var root = JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject;
        return root?[WebDavSyncPropertyName]?.DeepClone();
    }

    private static void RestoreWebDavSyncSettings(string settingsPath, JsonNode webDavSyncSettings)
    {
        if (webDavSyncSettings == null || !File.Exists(settingsPath))
        {
            return;
        }

        var root = JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject;
        if (root == null)
        {
            return;
        }

        root[WebDavSyncPropertyName] = webDavSyncSettings;
        File.WriteAllText(settingsPath, root.ToJsonString(JsonOptions));
    }

    internal static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, true);
        }
        catch (Exception e)
        {
            Log.Info(ClassName, $"Unable to delete directory {path}: {e.Message}");
        }
    }
}
