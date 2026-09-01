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
        foreach (var token in Tokenize(queryText))
        {
            if (QueryFilterCatalog.TryMatch(token, out var id, out var value) && id is not null)
            {
                snapshot.Set(id.Value, value);
            }
        }

        return snapshot;
    }

    internal static string Apply(string queryText, QueryFilterId id, string value, QueryFilterApplyMode mode)
    {
        var searchTokens = new List<string>();
        var filterTokens = new List<(QueryFilterId Id, string Value, string Token)>();

        foreach (var token in Tokenize(queryText))
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

        var group = QueryFilterCatalog.GroupOf(id);
        var remainingFilters = filterTokens
            .Where(filter => QueryFilterCatalog.GroupOf(filter.Id) != group)
            .Select(filter => filter.Token)
            .ToList();

        var current = filterTokens.FirstOrDefault(filter => filter.Id == id);
        var isActive = current.Token is not null;
        var sameValue = isActive && string.Equals(current.Value ?? string.Empty, value ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var shouldAdd = mode switch
        {
            QueryFilterApplyMode.Clear => false,
            QueryFilterApplyMode.Toggle when isActive && (string.IsNullOrEmpty(value) || sameValue) => false,
            _ => true
        };

        if (shouldAdd)
        {
            remainingFilters.Add(QueryFilterCatalog.Format(id, value));
        }

        return string.Join(' ', searchTokens.Concat(remainingFilters));
    }

    private static IEnumerable<string> Tokenize(string queryText)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return [];
        }

        return queryText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}
