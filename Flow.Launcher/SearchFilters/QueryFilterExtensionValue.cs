using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.SearchFilters;

internal static class QueryFilterExtensionValue
{
    private static readonly char[] Separators = [';', '|', ',', ' '];

    internal static IReadOnlyList<string> Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        var trimmed = input.Trim();
        if (trimmed.StartsWith("ext:", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[4..];
        }

        var values = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in trimmed.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryNormalizeOne(part, out var extension) || !seen.Add(extension))
            {
                continue;
            }

            values.Add(extension);
        }

        return values;
    }

    internal static IReadOnlyList<string> Union(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<string>();
        foreach (var value in values ?? [])
        {
            foreach (var extension in Parse(value))
            {
                if (seen.Add(extension))
                {
                    merged.Add(extension);
                }
            }
        }

        return merged;
    }

    internal static IReadOnlyList<string> Toggle(IEnumerable<string> current, string value)
    {
        if (!TryNormalizeOne(value, out var extension))
        {
            return Parse(string.Join(';', current ?? []));
        }

        var selected = Union(current).ToList();
        var index = selected.FindIndex(item => item.Equals(extension, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            selected.RemoveAt(index);
        }
        else
        {
            selected.Add(extension);
        }

        return selected;
    }

    internal static bool Contains(string current, string value) =>
        TryNormalizeOne(value, out var extension)
        && Parse(current).Any(item => item.Equals(extension, StringComparison.OrdinalIgnoreCase));

    internal static bool Equals(string left, string right)
    {
        var leftSet = Parse(left);
        var rightSet = Parse(right);
        return leftSet.Count == rightSet.Count
            && leftSet.All(item => rightSet.Contains(item, StringComparer.OrdinalIgnoreCase));
    }

    internal static string Join(IEnumerable<string> values)
    {
        var selected = new HashSet<string>(Union(values), StringComparer.OrdinalIgnoreCase);
        if (selected.Count == 0)
        {
            return string.Empty;
        }

        var ordered = QueryFilterCatalog.ExtensionPresets
            .Where(selected.Contains)
            .Concat(selected.Except(QueryFilterCatalog.ExtensionPresets, StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase));

        return string.Join(';', ordered);
    }

    internal static string ToDisplay(string value) =>
        string.Join(", ", Parse(value));

    internal static bool TryNormalizeOne(string input, out string extension)
    {
        extension = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim().TrimStart('.').ToLowerInvariant();
        if (trimmed.Length == 0 || trimmed.Length > 12)
        {
            return false;
        }

        foreach (var character in trimmed)
        {
            if (!char.IsAsciiLetterOrDigit(character))
            {
                return false;
            }
        }

        extension = trimmed;
        return true;
    }
}
