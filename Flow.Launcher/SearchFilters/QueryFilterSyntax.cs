using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.SearchFilters;

internal sealed class QueryFilterSnapshot
{
    private readonly Dictionary<QueryFilterId, string> _values = [];

    internal IReadOnlyDictionary<QueryFilterId, string> Values => _values;

    internal bool IsActive(QueryFilterId id) => _values.ContainsKey(id);

    internal string GetValue(QueryFilterId id) =>
        _values.TryGetValue(id, out var value) ? value : string.Empty;

    internal void Set(QueryFilterId id, string value)
    {
        var group = QueryFilterCatalog.GroupOf(id);
        foreach (var existing in _values.Keys.Where(existingId => QueryFilterCatalog.GroupOf(existingId) == group).ToList())
        {
            _values.Remove(existing);
        }

        _values[id] = value ?? string.Empty;
    }
}

internal static class QueryFilterSyntax
{
    internal static QueryFilterSnapshot Parse(string queryText)
    {
        var snapshot = new QueryFilterSnapshot();
        var body = queryText;
        if (QueryFilterPathValue.TrySplitScope(queryText, out var scopedPath, out var remainder))
        {
            snapshot.Set(QueryFilterId.Path, scopedPath);
            body = remainder;
        }

        foreach (var token in Tokenize(body))
        {
            if (QueryFilterCatalog.TryMatch(token, out var id, out var value) && id is not null)
            {
                if (snapshot.IsActive(QueryFilterId.Path) && QueryFilterCatalog.GroupOf(id.Value) == QueryFilterGroup.Path)
                {
                    continue;
                }

                if (id.Value == QueryFilterId.Extension && snapshot.IsActive(QueryFilterId.Extension))
                {
                    snapshot.Set(
                        QueryFilterId.Extension,
                        QueryFilterExtensionValue.Join(
                        [
                            snapshot.GetValue(QueryFilterId.Extension),
                            value
                        ]));
                    continue;
                }

                snapshot.Set(id.Value, value);
            }
        }

        return snapshot;
    }

    internal static string Apply(string queryText, QueryFilterId id, string value, QueryFilterApplyMode mode)
    {
        var hasScope = QueryFilterPathValue.TrySplitScope(queryText, out var existingPath, out var remainder);
        var body = hasScope ? remainder : queryText;

        var searchTokens = new List<string>();
        var filterTokens = new List<(QueryFilterId Id, string Value, string Token)>();

        foreach (var token in Tokenize(body))
        {
            if (QueryFilterCatalog.TryMatch(token, out var existingId, out var existingValue) && existingId is not null)
            {
                filterTokens.Add((existingId.Value, existingValue, token));
            }
            else
            {
                searchTokens.Add(token);
            }
        }

        if (id == QueryFilterId.Size && !string.IsNullOrEmpty(value))
        {
            if (!QueryFilterSizeValue.TryNormalize(value, out var normalizedSize))
            {
                return queryText;
            }

            value = normalizedSize;
        }

        if (id == QueryFilterId.Path && !string.IsNullOrEmpty(value))
        {
            if (!QueryFilterPathValue.TryNormalize(value, out var normalizedPath))
            {
                return queryText;
            }

            value = normalizedPath;
        }

        var group = QueryFilterCatalog.GroupOf(id);
        var remainingFilters = filterTokens
            .Where(filter => QueryFilterCatalog.GroupOf(filter.Id) != group)
            .Select(filter => filter.Token)
            .ToList();

        var current = filterTokens.FirstOrDefault(filter => filter.Id == id);
        var isActive = current.Token is not null;
        var sameValue = isActive && ValuesEqual(id, current.Value, value);
        if (id == QueryFilterId.Path)
        {
            isActive = hasScope || isActive;
            sameValue = isActive && ValuesEqual(id, hasScope ? existingPath : current.Value, value);
        }

        var shouldAdd = mode switch
        {
            QueryFilterApplyMode.Clear => false,
            QueryFilterApplyMode.Toggle when isActive && (string.IsNullOrEmpty(value) || sameValue) => false,
            _ => true
        };

        if (shouldAdd && id == QueryFilterId.Path && string.IsNullOrEmpty(value))
        {
            shouldAdd = false;
        }

        if (id == QueryFilterId.Extension)
        {
            var existingExtensions = QueryFilterExtensionValue.Union(
                filterTokens.Where(filter => filter.Id == QueryFilterId.Extension).Select(filter => filter.Value));
            IReadOnlyList<string> nextExtensions;
            if (mode == QueryFilterApplyMode.Clear || string.IsNullOrEmpty(value))
            {
                nextExtensions = [];
            }
            else if (mode == QueryFilterApplyMode.Toggle)
            {
                nextExtensions = QueryFilterExtensionValue.Toggle(existingExtensions, value);
            }
            else
            {
                nextExtensions = QueryFilterExtensionValue.Parse(value);
            }

            if (nextExtensions.Count > 0)
            {
                remainingFilters.Add(
                    QueryFilterCatalog.Format(QueryFilterId.Extension, QueryFilterExtensionValue.Join(nextExtensions)));
            }

            var extensionBody = string.Join(' ', searchTokens.Concat(remainingFilters));
            return hasScope ? QueryFilterPathValue.Combine(existingPath, extensionBody) : extensionBody;
        }

        if (id == QueryFilterId.Path)
        {
            var pathBody = string.Join(' ', searchTokens.Concat(remainingFilters));
            return shouldAdd ? QueryFilterPathValue.Combine(value, pathBody) : pathBody;
        }

        if (shouldAdd)
        {
            remainingFilters.Add(QueryFilterCatalog.Format(id, value));
        }

        var rest = string.Join(' ', searchTokens.Concat(remainingFilters));
        return hasScope ? QueryFilterPathValue.Combine(existingPath, rest) : rest;
    }

    private static IEnumerable<string> Tokenize(string queryText)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return [];
        }

        var tokens = new List<string>();
        var index = 0;
        var length = queryText.Length;
        while (index < length)
        {
            while (index < length && char.IsWhiteSpace(queryText[index]))
            {
                index++;
            }

            if (index >= length)
            {
                break;
            }

            var start = index;
            var inQuotes = false;
            while (index < length)
            {
                var current = queryText[index];
                if (current == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (!inQuotes && char.IsWhiteSpace(current))
                {
                    break;
                }

                index++;
            }

            tokens.Add(queryText[start..index]);
        }

        return tokens;
    }

    private static bool ValuesEqual(QueryFilterId id, string left, string right)
    {
        if (id == QueryFilterId.Size)
        {
            return QueryFilterSizeValue.Equals(left, right);
        }

        if (id == QueryFilterId.Path)
        {
            return QueryFilterPathValue.Equals(left, right);
        }

        if (id == QueryFilterId.Extension)
        {
            return QueryFilterExtensionValue.Equals(left, right);
        }

        return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
