using System;

namespace Flow.Launcher.SearchFilters;

internal static class QueryFilterSizeSteps
{
    internal const int AnyIndex = 0;

    internal static readonly string[] Tokens =
    [
        "",
        "1kb",
        "10kb",
        "100kb",
        "1mb",
        "2mb",
        "5mb",
        "10mb",
        "20mb",
        "50mb",
        "100mb",
        "200mb",
        "500mb",
        "1gb",
        "2gb",
        "5gb",
        "10gb",
        "50gb",
        "100gb"
    ];

    internal static int LastIndex => Tokens.Length - 1;

    internal static string TokenAt(int index) =>
        index <= AnyIndex || index >= Tokens.Length ? string.Empty : Tokens[index];

    internal static int IndexOf(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return AnyIndex;
        }

        for (var i = 1; i < Tokens.Length; i++)
        {
            if (QueryFilterSizeValue.Equals(Tokens[i], token))
            {
                return i;
            }
        }

        if (!QueryFilterSizeValue.TryGetBytes(token, out var bytes))
        {
            return AnyIndex;
        }

        var best = 1;
        var bestDiff = long.MaxValue;
        for (var i = 1; i < Tokens.Length; i++)
        {
            if (!QueryFilterSizeValue.TryGetBytes(Tokens[i], out var stepBytes))
            {
                continue;
            }

            var diff = Math.Abs(stepBytes - bytes);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                best = i;
            }
        }

        return best;
    }
}
