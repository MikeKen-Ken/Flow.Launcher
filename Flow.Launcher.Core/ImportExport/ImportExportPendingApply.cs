using System;
using System.IO;
using System.Text.Json;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Core.ImportExport;

public static class ImportExportPendingApply
{
    private static readonly string ClassName = nameof(ImportExportPendingApply);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string GetPendingDirectory(string dataDirectory) =>
        Path.Combine(dataDirectory, ImportExportConstants.FolderName, ImportExportConstants.PendingDirectoryName);

    public static string GetPendingZipPath(string dataDirectory) =>
        Path.Combine(dataDirectory, ImportExportConstants.FolderName, ImportExportConstants.PendingZipFileName);

    public static string GetPendingOptionsPath(string dataDirectory) =>
        Path.Combine(dataDirectory, ImportExportConstants.FolderName, ImportExportConstants.PendingOptionsFileName);

    public static void StageZip(string zipPath, string dataDirectory, bool applySettings, bool applyPlugins)
    {
        ClearPending(dataDirectory);
        var pendingPath = GetPendingZipPath(dataDirectory);
        EnsureParentDirectory(pendingPath);
        File.Copy(zipPath, pendingPath, overwrite: true);
        WriteOptions(dataDirectory, applySettings, applyPlugins);
    }

    public static void StageDirectory(string packageDirectory, string dataDirectory, bool applySettings, bool applyPlugins)
    {
        ClearPending(dataDirectory);
        var pendingDirectory = GetPendingDirectory(dataDirectory);
        ImportExportFileCopy.CopyDirectory(packageDirectory, pendingDirectory, overwrite: true);
        WriteOptions(dataDirectory, applySettings, applyPlugins);
    }

    public static bool ApplyIfNeeded() => ApplyIfNeeded(DataLocation.DataDirectory());

    public static bool ApplyIfNeeded(string dataDirectory)
    {
        var pendingZip = GetPendingZipPath(dataDirectory);
        var pendingDirectory = GetPendingDirectory(dataDirectory);
        var hasZip = File.Exists(pendingZip);
        var hasDirectory = Directory.Exists(pendingDirectory);
        if (!hasZip && !hasDirectory)
        {
            return false;
        }

        try
        {
            var options = ReadOptions(dataDirectory);
            var paths = new ImportExportPaths
            {
                DataDirectory = dataDirectory,
                SettingsDirectory = Path.Combine(dataDirectory, Constant.Settings),
                PluginsDirectory = Path.Combine(dataDirectory, Constant.Plugins),
                ThemesDirectory = Path.Combine(dataDirectory, Constant.Themes)
            };

            if (hasZip)
            {
                ImportExportPackage.ApplyFromZip(pendingZip, paths, options.ApplySettings, options.ApplyPlugins);
            }
            else
            {
                ImportExportPackage.ApplyFromDirectory(
                    pendingDirectory, paths, options.ApplySettings, options.ApplyPlugins);
            }

            ClearPending(dataDirectory);
            Log.Info(ClassName, "Applied pending import package.");
            return true;
        }
        catch (Exception e)
        {
            Log.Error(ClassName, $"Failed to apply pending import package: {e.Message}");
            return false;
        }
    }

    private static void WriteOptions(string dataDirectory, bool applySettings, bool applyPlugins)
    {
        var optionsPath = GetPendingOptionsPath(dataDirectory);
        EnsureParentDirectory(optionsPath);
        var options = new ImportExportPendingOptions
        {
            ApplySettings = applySettings,
            ApplyPlugins = applyPlugins
        };
        File.WriteAllText(optionsPath, JsonSerializer.Serialize(options, JsonOptions));
    }

    private static ImportExportPendingOptions ReadOptions(string dataDirectory)
    {
        var optionsPath = GetPendingOptionsPath(dataDirectory);
        if (!File.Exists(optionsPath))
        {
            return new ImportExportPendingOptions { ApplySettings = true, ApplyPlugins = true };
        }

        return JsonSerializer.Deserialize<ImportExportPendingOptions>(File.ReadAllText(optionsPath), JsonOptions)
               ?? new ImportExportPendingOptions { ApplySettings = true, ApplyPlugins = true };
    }

    private static void ClearPending(string dataDirectory)
    {
        ImportExportFileCopy.TryDeleteFile(GetPendingZipPath(dataDirectory));
        ImportExportFileCopy.TryDeleteFile(GetPendingOptionsPath(dataDirectory));
        ImportExportFileCopy.TryDeleteDirectory(GetPendingDirectory(dataDirectory));
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private sealed class ImportExportPendingOptions
    {
        public bool ApplySettings { get; set; }

        public bool ApplyPlugins { get; set; }
    }
}
