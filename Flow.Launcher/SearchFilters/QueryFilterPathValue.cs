using System;
using System.IO;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.SearchFilters;

internal static class QueryFilterPathValue
{
    internal static bool TryNormalize(string input, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = StripQuotes(input.Trim());
        if (trimmed.StartsWith("path:", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = StripQuotes(trimmed[5..].Trim());
        }

        if (trimmed.EndsWith('>'))
        {
            trimmed = trimmed[..^1].TrimEnd();
        }

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        path = TrimTrailingSlashes(trimmed.Replace('/', '\\'));
        return path.Length > 0;
    }

    internal static bool Equals(string left, string right)
    {
        var leftOk = TryNormalize(left ?? string.Empty, out var normalizedLeft);
        var rightOk = TryNormalize(right ?? string.Empty, out var normalizedRight);
        if (leftOk && rightOk)
        {
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    internal static string FormatCommand(string value)
    {
        if (!TryNormalize(value, out var path))
        {
            return string.Empty;
        }

        return WithDirectorySlash(path) + ">";
    }

    internal static bool TrySplitScope(string queryText, out string path, out string remainder)
    {
        path = string.Empty;
        remainder = queryText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return false;
        }

        var text = queryText.TrimStart();
        var index = text.IndexOf('>');
        if (index <= 0)
        {
            return false;
        }

        var rawPath = text[..index].TrimEnd();
        if (!rawPath.IsLocationPathString() || !TryNormalize(rawPath, out path))
        {
            return false;
        }

        remainder = text[(index + 1)..];
        return true;
    }

    internal static string Combine(string path, string remainder)
    {
        var command = FormatCommand(path);
        if (string.IsNullOrEmpty(command))
        {
            return remainder ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(remainder))
        {
            return command;
        }

        return command + remainder.TrimStart();
    }

    internal static string ToDisplay(string value)
    {
        if (!TryNormalize(value, out var path))
        {
            return value;
        }

        var name = Path.GetFileName(path);
        return string.IsNullOrEmpty(name) ? path : name;
    }

    private static string StripQuotes(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1];
        }

        return value;
    }

    private static string TrimTrailingSlashes(string path)
    {
        if (path.Length <= 1)
        {
            return path;
        }

        var end = path.Length;
        while (end > 1 && path[end - 1] is '\\')
        {
            if (end == 3 && char.IsLetter(path[0]) && path[1] == ':')
            {
                break;
            }

            if (end == 2 && path[0] == '\\')
            {
                break;
            }

            end--;
        }

        return path[..end];
    }

    private static string WithDirectorySlash(string path) =>
        path.EndsWith('\\') ? path : path + '\\';
}
