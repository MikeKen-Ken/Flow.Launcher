using System;
using System.IO;

namespace Flow.Launcher.ShellFolderSearch;

/// <summary>
/// Command-line contract for opening Flow Launcher scoped to a folder.
/// Matches Explorer plugin path search: <c>C:\folder\&gt;</c> searches recursively inside that folder.
/// </summary>
public static class FolderSearchCommand
{
    public const string SearchFolderSwitch = "--search-folder";

    /// <summary>
    /// Explorer plugin recursive-search indicator (see
    /// <c>Flow.Launcher.Plugin.Explorer.Search.Constants.AllFilesFolderSearchWildcard</c>).
    /// </summary>
    public const char RecursiveSearchIndicator = '>';

    public static bool TryGetFolder(string[] args, out string folderPath)
    {
        folderPath = null;
        if (args is null || args.Length == 0)
            return false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.IsNullOrEmpty(arg))
                continue;

            string candidate = null;
            if (arg.Equals(SearchFolderSwitch, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                    return false;
                candidate = args[i + 1];
            }
            else if (arg.StartsWith(SearchFolderSwitch + "=", StringComparison.OrdinalIgnoreCase))
            {
                candidate = arg[(SearchFolderSwitch.Length + 1)..];
            }

            if (candidate is null)
                continue;

            if (!TryNormalizeFolderPath(candidate, out folderPath))
                return false;

            return true;
        }

        return false;
    }

    public static string BuildQuery(string folderPath)
    {
        if (!TryNormalizeFolderPath(folderPath, out var normalized))
            return string.Empty;

        // The separator before '>' is part of the Explorer plugin contract. Without it,
        // the final folder name is interpreted as a search pattern in the parent folder.
        return normalized.TrimEnd('\\') + "\\" + RecursiveSearchIndicator;
    }

    public static string BuildCommandLine(string executablePath, string pathPlaceholder)
    {
        return $"\"{executablePath}\" {SearchFolderSwitch} \"{pathPlaceholder}\"";
    }

    internal static bool TryNormalizeFolderPath(string folderPath, out string normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(folderPath))
            return false;

        var trimmed = folderPath.Trim().Trim('"');
        if (trimmed.Length == 0)
            return false;

        trimmed = trimmed.Replace('/', '\\');

        if (trimmed.Length == 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':')
            trimmed += '\\';

        if (!Path.IsPathRooted(trimmed))
            return false;

        try
        {
            normalized = Path.GetFullPath(trimmed);
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }
}
