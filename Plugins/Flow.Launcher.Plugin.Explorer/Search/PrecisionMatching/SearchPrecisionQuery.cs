using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.Explorer.Search.PrecisionMatching;

internal enum NameMatchMode
{
    Contains,
    Exact,
    Prefix,
    Suffix,
    WholeWord
}

internal sealed record SearchPrecisionQuery(
    string ProviderSearch,
    string MatchText,
    IReadOnlyList<string> MatchTerms,
    NameMatchMode Mode,
    bool CaseSensitive,
    IReadOnlyList<string> Extensions)
{
    internal bool RequiresPostFilter =>
        !string.IsNullOrEmpty(MatchText) && (CaseSensitive || Mode != NameMatchMode.Contains);

    internal static SearchPrecisionQuery Parse(string search)
    {
        var providerTokens = new List<string>();
        var mode = NameMatchMode.Contains;
        var caseSensitive = false;

        foreach (var token in Tokenize(search))
        {
            if (TryReadControl(token, out var nextMode, out var enablesCase))
            {
                if (nextMode is not null)
                {
                    mode = nextMode.Value;
                }

                caseSensitive |= enablesCase;
                continue;
            }

            providerTokens.Add(token);
        }

        var providerSearch = string.Join(' ', providerTokens);
        var bodySeparator = providerSearch.IndexOf('>');
        var nameBody = bodySeparator >= 0 ? providerSearch[(bodySeparator + 1)..] : providerSearch;
        var nameTokens = new List<string>();
        var extensions = new List<string>();

        foreach (var token in Tokenize(nameBody))
        {
            if (TryReadExtensions(token, extensions) || IsPropertyFilter(token))
            {
                continue;
            }

            nameTokens.Add(Unquote(token));
        }

        return new SearchPrecisionQuery(
            providerSearch,
            string.Join(' ', nameTokens).Trim(),
            nameTokens,
            mode,
            caseSensitive,
            extensions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static bool TryReadControl(string token, out NameMatchMode? mode, out bool enablesCase)
    {
        mode = null;
        enablesCase = false;

        if (token.Equals("case:", StringComparison.OrdinalIgnoreCase))
        {
            enablesCase = true;
            return true;
        }

        mode = token.ToLowerInvariant() switch
        {
            "match:exact" or "exact:" => NameMatchMode.Exact,
            "match:prefix" or "prefix:" => NameMatchMode.Prefix,
            "match:suffix" or "suffix:" => NameMatchMode.Suffix,
            "match:word" or "wholeword:" or "wholewords:" or "ww:" => NameMatchMode.WholeWord,
            _ => null
        };
        return mode is not null;
    }

    private static bool IsPropertyFilter(string token)
    {
        var colon = token.IndexOf(':');
        if (colon < 1)
        {
            return token is "|" or "!";
        }

        var prefix = token[..colon];
        return prefix.Equals("file", StringComparison.OrdinalIgnoreCase)
            || prefix.Equals("files", StringComparison.OrdinalIgnoreCase)
            || prefix.Equals("folder", StringComparison.OrdinalIgnoreCase)
            || prefix.Equals("folders", StringComparison.OrdinalIgnoreCase)
            || prefix.Equals("size", StringComparison.OrdinalIgnoreCase)
            || prefix.Equals("dm", StringComparison.OrdinalIgnoreCase)
            || prefix.Equals("datemodified", StringComparison.OrdinalIgnoreCase)
            || prefix.Equals("dc", StringComparison.OrdinalIgnoreCase)
            || prefix.Equals("datecreated", StringComparison.OrdinalIgnoreCase)
            || prefix.Equals("da", StringComparison.OrdinalIgnoreCase)
            || prefix.Equals("dateaccessed", StringComparison.OrdinalIgnoreCase)
            || prefix.Equals("attrib", StringComparison.OrdinalIgnoreCase)
            || prefix.Equals("attributes", StringComparison.OrdinalIgnoreCase)
            || prefix.Equals("path", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadExtensions(string token, ICollection<string> extensions)
    {
        var colon = token.IndexOf(':');
        if (colon < 1 || !token[..colon].Equals("ext", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var extension in token[(colon + 1)..].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = extension.TrimStart('.', '*');
            if (normalized.Length > 0)
            {
                extensions.Add(normalized);
            }
        }

        return true;
    }

    private static string Unquote(string token) =>
        token.Length >= 2 && token[0] == '"' && token[^1] == '"' ? token[1..^1] : token;

    private static IEnumerable<string> Tokenize(string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            yield break;
        }

        var index = 0;
        while (index < search.Length)
        {
            while (index < search.Length && char.IsWhiteSpace(search[index]))
            {
                index++;
            }

            if (index >= search.Length)
            {
                yield break;
            }

            var start = index;
            var inQuotes = false;
            while (index < search.Length)
            {
                if (search[index] == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (!inQuotes && char.IsWhiteSpace(search[index]))
                {
                    break;
                }

                index++;
            }

            yield return search[start..index];
        }
    }
}
