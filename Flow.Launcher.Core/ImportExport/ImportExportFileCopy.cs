using System;
using System.IO;
using System.Threading;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Core.ImportExport;

internal static class ImportExportFileCopy
{
    private static readonly string ClassName = nameof(ImportExportFileCopy);

    public static void CopyDirectory(
        string source,
        string destination,
        bool overwrite = false,
        CancellationToken token = default)
    {
        if (!Directory.Exists(source))
        {
            return;
        }

        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested();
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

    public static void ReplaceDirectory(
        string source,
        string destination,
        CancellationToken token = default)
    {
        if (!Directory.Exists(source))
        {
            return;
        }

        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, recursive: true);
        }

        CopyDirectory(source, destination, overwrite: true, token);
    }

    public static void CopyFile(string source, string destination, bool overwrite)
    {
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var output = new FileStream(
            destination,
            overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        input.CopyTo(output);
    }

    public static void TryDeleteDirectory(string path)
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

    public static void TryDeleteFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception e)
        {
            Log.Info(ClassName, $"Unable to delete file {path}: {e.Message}");
        }
    }
}
