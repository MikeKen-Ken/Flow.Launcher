using System;
using System.IO;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Core.WebDavSync;

public static class WebDavPendingApply
{
    private static readonly string ClassName = nameof(WebDavPendingApply);

    public static string GetPendingZipPath(string dataDirectory) =>
        Path.Combine(dataDirectory, WebDavSyncConstants.FolderName, WebDavSyncConstants.PendingZipFileName);

    public static void StageDownloadedZip(string downloadedZipPath, string dataDirectory)
    {
        var pendingPath = GetPendingZipPath(dataDirectory);
        var pendingDirectory = Path.GetDirectoryName(pendingPath);
        if (!string.IsNullOrEmpty(pendingDirectory))
        {
            Directory.CreateDirectory(pendingDirectory);
        }

        if (File.Exists(pendingPath))
        {
            File.Delete(pendingPath);
        }

        File.Copy(downloadedZipPath, pendingPath, overwrite: true);
    }

    public static bool ApplyIfNeeded()
    {
        return ApplyIfNeeded(DataLocation.DataDirectory());
    }

    public static bool ApplyIfNeeded(string dataDirectory)
    {
        var pendingPath = GetPendingZipPath(dataDirectory);
        if (!File.Exists(pendingPath))
        {
            return false;
        }

        try
        {
            var paths = new WebDavSyncPaths
            {
                DataDirectory = dataDirectory,
                SettingsDirectory = Path.Combine(dataDirectory, Constant.Settings),
                PluginsDirectory = Path.Combine(dataDirectory, Constant.Plugins),
                ThemesDirectory = Path.Combine(dataDirectory, Constant.Themes)
            };

            WebDavSyncArchive.ApplyFromZip(pendingPath, paths, applySettings: true, applyPlugins: true);
            WebDavSyncSettingsStamp.MarkAppliedNow(paths.SettingsDirectory);
            File.Delete(pendingPath);
            Log.Info(ClassName, "Applied pending WebDAV sync package.");
            return true;
        }
        catch (Exception e)
        {
            Log.Error(ClassName, $"Failed to apply pending WebDAV sync package: {e.Message}");
            return false;
        }
    }
}
