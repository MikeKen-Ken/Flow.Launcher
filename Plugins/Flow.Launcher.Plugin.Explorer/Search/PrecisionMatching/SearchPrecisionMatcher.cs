using System;
using System.IO;
using System.Linq;

namespace Flow.Launcher.Plugin.Explorer.Search.PrecisionMatching;

internal static class SearchPrecisionMatcher
{
    internal static bool IsMatch(SearchResult result, SearchPrecisionQuery query)
    {
        if (!query.RequiresPostFilter || string.IsNullOrEmpty(result.FullPath))
        {
            return true;
        }

        var candidate = Path.GetFileName(result.FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var comparison = query.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var matchText = query.MatchText;

        if (query.CaseSensitive && query.Extensions.Count > 0)
        {
            var candidateExtension = Path.GetExtension(candidate).TrimStart('.');
            if (!query.Extensions.Contains(candidateExtension, StringComparer.Ordinal))
            {
                return false;
            }
        }

        if (query.Extensions.Count > 0 && !HasRequestedExtension(matchText, query.Extensions))
        {
            candidate = Path.GetFileNameWithoutExtension(candidate);
        }

        return query.Mode switch
        {
            NameMatchMode.Exact => candidate.Equals(matchText, comparison),
            NameMatchMode.Prefix => candidate.StartsWith(matchText, comparison),
            NameMatchMode.Suffix => candidate.EndsWith(matchText, comparison),
            NameMatchMode.WholeWord => ContainsWholeWord(candidate, matchText, comparison),
            _ => query.MatchTerms.All(term => candidate.Contains(term, comparison))
        };
    }

    private static bool HasRequestedExtension(string value, System.Collections.Generic.IReadOnlyList<string> extensions)
    {
        foreach (var extension in extensions)
        {
            if (value.EndsWith($".{extension}", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsWholeWord(string candidate, string matchText, StringComparison comparison)
    {
        var start = 0;
        while (start <= candidate.Length - matchText.Length)
        {
            var match = candidate.IndexOf(matchText, start, comparison);
            if (match < 0)
            {
                return false;
            }

            var beforeIsWord = match > 0 && char.IsLetterOrDigit(candidate[match - 1]);
            var after = match + matchText.Length;
            var afterIsWord = after < candidate.Length && char.IsLetterOrDigit(candidate[after]);
            if (!beforeIsWord && !afterIsWord)
            {
                return true;
            }

            start = match + 1;
        }

        return false;
    }
}
