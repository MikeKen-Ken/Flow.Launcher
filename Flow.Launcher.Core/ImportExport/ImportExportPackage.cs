using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using Flow.Launcher.Infrastructure;

namespace Flow.Launcher.Core.ImportExport;

public static class ImportExportPackage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static ImportExportManifest WriteToDirectory(
        string destinationDirectory,
        ImportExportPaths paths,
        bool includeSettings,
        bool includePlugins,
        CancellationToken token = default)
    {
        Directory.CreateDirectory(destinationDirectory);
        var manifest = CreateManifest(includeSettings, includePlugins);
        token.ThrowIfCancellationRequested();

        if (includeSettings)
        {
            ImportExportFileCopy.CopyDirectory(
                paths.SettingsDirectory,
                Path.Combine(destinationDirectory, Constant.Settings),
                overwrite: true,
                token);
            ImportExportFileCopy.CopyDirectory(
                paths.ThemesDirectory,
                Path.Combine(destinationDirectory, Constant.Themes),
                overwrite: true,
                token);
        }

        if (includePlugins)
        {
            ImportExportFileCopy.CopyDirectory(
                paths.PluginsDirectory,
                Path.Combine(destinationDirectory, Constant.Plugins),
                overwrite: true,
                token);
        }

        File.WriteAllText(
            Path.Combine(destinationDirectory, ImportExportConstants.ManifestFileName),
            JsonSerializer.Serialize(manifest, JsonOptions));
        return manifest;
    }

    public static ImportExportManifest CreateZip(
        string zipPath,
        ImportExportPaths paths,
        bool includeSettings,
        bool includePlugins,
        CancellationToken token = default)
    {
        var staging = Path.Combine(Path.GetTempPath(), "FlowLauncherImportExportPack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            var manifest = WriteToDirectory(staging, paths, includeSettings, includePlugins, token);
            token.ThrowIfCancellationRequested();

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
            ImportExportFileCopy.TryDeleteDirectory(staging);
        }
    }

    public static ImportExportManifest ReadManifestFromZip(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var inferred = InferManifestFromZip(archive, zipPath);
        var entry = archive.GetEntry(ImportExportConstants.ManifestFileName);
        if (entry == null)
        {
            return inferred;
        }

        using var stream = entry.Open();
        var manifest = JsonSerializer.Deserialize<ImportExportManifest>(stream, JsonOptions) ?? inferred;
        MergeInferredFlags(manifest, inferred);
        return manifest;
    }

    public static ImportExportManifest ReadManifestFromDirectory(string directory)
    {
        var inferred = InferManifestFromDirectory(directory);
        var manifestPath = Path.Combine(directory, ImportExportConstants.ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return inferred;
        }

        var manifest = JsonSerializer.Deserialize<ImportExportManifest>(File.ReadAllText(manifestPath), JsonOptions)
                       ?? inferred;
        MergeInferredFlags(manifest, inferred);
        return manifest;
    }

    public static ImportExportManifest ExtractToDirectory(string zipPath, string destinationDirectory)
    {
        if (Directory.Exists(destinationDirectory))
        {
            ImportExportFileCopy.TryDeleteDirectory(destinationDirectory);
        }

        Directory.CreateDirectory(destinationDirectory);
        ZipFile.ExtractToDirectory(zipPath, destinationDirectory);
        return ReadManifestFromDirectory(destinationDirectory);
    }

    public static void ApplyFromDirectory(
        string packageDirectory,
        ImportExportPaths paths,
        bool applySettings,
        bool applyPlugins)
    {
        if (applySettings && HasSettings(packageDirectory))
        {
            ImportExportFileCopy.CopyDirectory(
                Path.Combine(packageDirectory, Constant.Settings),
                paths.SettingsDirectory,
                overwrite: true);
            ImportExportFileCopy.CopyDirectory(
                Path.Combine(packageDirectory, Constant.Themes),
                paths.ThemesDirectory,
                overwrite: true);
        }

        if (applyPlugins && HasPlugins(packageDirectory))
        {
            ImportExportFileCopy.ReplaceDirectory(
                Path.Combine(packageDirectory, Constant.Plugins),
                paths.PluginsDirectory);
        }
    }

    public static void ApplyFromZip(
        string zipPath,
        ImportExportPaths paths,
        bool applySettings,
        bool applyPlugins)
    {
        var staging = Path.Combine(Path.GetTempPath(), "FlowLauncherImportExportApply-" + Guid.NewGuid().ToString("N"));
        try
        {
            ExtractToDirectory(zipPath, staging);
            ApplyFromDirectory(staging, paths, applySettings, applyPlugins);
        }
        finally
        {
            ImportExportFileCopy.TryDeleteDirectory(staging);
        }
    }

    public static bool IsPackageDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        return File.Exists(Path.Combine(directory, ImportExportConstants.ManifestFileName))
               || HasSettings(directory)
               || HasPlugins(directory);
    }

    public static bool IsPackageZip(string zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
        {
            return false;
        }

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            return archive.GetEntry(ImportExportConstants.ManifestFileName) != null
                   || HasZipEntry(archive, Constant.Settings)
                   || HasZipEntry(archive, Constant.Plugins);
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    public static bool HasSettings(string packageDirectory) =>
        Directory.Exists(Path.Combine(packageDirectory, Constant.Settings));

    public static bool HasPlugins(string packageDirectory) =>
        Directory.Exists(Path.Combine(packageDirectory, Constant.Plugins));

    public static bool OverlapsDataPaths(string directory, ImportExportPaths paths)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        var fullDirectory = Path.GetFullPath(directory);
        return IsSameOrChild(fullDirectory, paths.DataDirectory)
               || IsSameOrChild(fullDirectory, paths.SettingsDirectory)
               || IsSameOrChild(fullDirectory, paths.PluginsDirectory)
               || IsSameOrChild(fullDirectory, paths.ThemesDirectory)
               || IsSameOrChild(paths.SettingsDirectory, Path.Combine(fullDirectory, Constant.Settings))
               || IsSameOrChild(paths.PluginsDirectory, Path.Combine(fullDirectory, Constant.Plugins))
               || IsSameOrChild(paths.ThemesDirectory, Path.Combine(fullDirectory, Constant.Themes));
    }

    private static void MergeInferredFlags(ImportExportManifest manifest, ImportExportManifest inferred)
    {
        manifest.IncludesSettings |= inferred.IncludesSettings;
        manifest.IncludesPlugins |= inferred.IncludesPlugins;
    }

    private static ImportExportManifest CreateManifest(bool includeSettings, bool includePlugins) => new()
    {
        Version = ImportExportConstants.ManifestVersion,
        ExportedAtUtc = DateTime.UtcNow,
        DeviceName = Environment.MachineName,
        IncludesSettings = includeSettings,
        IncludesPlugins = includePlugins
    };

    private static ImportExportManifest InferManifestFromDirectory(string directory) => new()
    {
        Version = ImportExportConstants.ManifestVersion,
        ExportedAtUtc = Directory.Exists(directory) ? Directory.GetLastWriteTimeUtc(directory) : DateTime.UtcNow,
        IncludesSettings = HasSettings(directory),
        IncludesPlugins = HasPlugins(directory)
    };

    private static ImportExportManifest InferManifestFromZip(ZipArchive archive, string zipPath) => new()
    {
        Version = ImportExportConstants.ManifestVersion,
        ExportedAtUtc = File.GetLastWriteTimeUtc(zipPath),
        IncludesSettings = HasZipEntry(archive, Constant.Settings),
        IncludesPlugins = HasZipEntry(archive, Constant.Plugins)
    };

    private static bool HasZipEntry(ZipArchive archive, string folderName)
    {
        var prefixForward = folderName + "/";
        var prefixBack = folderName + "\\";
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName;
            if (name.StartsWith(prefixForward, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(prefixBack, StringComparison.OrdinalIgnoreCase)
                || name.Equals(folderName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSameOrChild(string candidate, string root)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var fullCandidate = Path.GetFullPath(candidate)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(fullCandidate, fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = fullRoot + Path.DirectorySeparatorChar;
        return fullCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
