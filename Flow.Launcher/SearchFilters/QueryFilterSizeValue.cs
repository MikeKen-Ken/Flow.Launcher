using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Flow.Launcher.SearchFilters;

internal static class QueryFilterSizeValue
{
    private static readonly Regex SizeExpression = new(
        @"^(?<op>>=|<=|>|<)?\s*(?<n1>\d+(?:\.\d+)?)\s*(?<u1>kb|mb|gb|tb|k|m|g|t|b)?\s*(?:\.\.\s*(?<n2>\d+(?:\.\d+)?)\s*(?<u2>kb|mb|gb|tb|k|m|g|t|b)?\s*)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryNormalize(string input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (trimmed.StartsWith("size:", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[5..].Trim();
        }

        if (IsNamedSize(trimmed))
        {
            normalized = trimmed.ToLowerInvariant();
            return true;
        }

        var match = SizeExpression.Match(trimmed);
        if (!match.Success)
        {
            return false;
        }

        var op = match.Groups["op"].Value;
        var first = FormatPart(match.Groups["n1"].Value, match.Groups["u1"].Value);
        var secondNumber = match.Groups["n2"].Value;
        if (string.IsNullOrEmpty(secondNumber))
        {
            normalized = op + first;
            return true;
        }

        if (!string.IsNullOrEmpty(op))
        {
            return false;
        }

        var second = FormatPart(secondNumber, match.Groups["u2"].Value);
        normalized = $"{first}..{second}";
        return true;
    }

    internal static bool TryParseBounds(string input, out string min, out string max)
    {
        min = string.Empty;
        max = string.Empty;
        if (string.IsNullOrWhiteSpace(input)
            || !TryNormalize(input, out var normalized)
            || IsNamedSize(normalized))
        {
            return false;
        }

        var dots = normalized.IndexOf("..", StringComparison.Ordinal);
        if (dots >= 0)
        {
            min = normalized[..dots];
            max = normalized[(dots + 2)..];
            return min.Length > 0 && max.Length > 0;
        }

        if (normalized.StartsWith(">=", StringComparison.Ordinal) || normalized.StartsWith('>'))
        {
            min = normalized.StartsWith(">=", StringComparison.Ordinal) ? normalized[2..] : normalized[1..];
            return min.Length > 0;
        }

        if (normalized.StartsWith("<=", StringComparison.Ordinal) || normalized.StartsWith('<'))
        {
            max = normalized.StartsWith("<=", StringComparison.Ordinal) ? normalized[2..] : normalized[1..];
            return max.Length > 0;
        }

        return false;
    }

    internal static string FormatBounds(string min, string max)
    {
        var hasMin = TryNormalizeBoundPart(min, out var minPart);
        var hasMax = TryNormalizeBoundPart(max, out var maxPart);
        if (!hasMin && !hasMax)
        {
            return string.Empty;
        }

        if (hasMin && hasMax)
        {
            if (TryGetBytes(minPart, out var minBytes)
                && TryGetBytes(maxPart, out var maxBytes)
                && minBytes > maxBytes)
            {
                (minPart, maxPart) = (maxPart, minPart);
            }

            return $"{minPart}..{maxPart}";
        }

        return hasMin ? ">" + minPart : "<" + maxPart;
    }

    internal static bool TryGetBytes(string input, out long bytes)
    {
        bytes = 0;
        if (string.IsNullOrWhiteSpace(input)
            || !TryNormalize(input, out var normalized)
            || IsNamedSize(normalized)
            || normalized.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        var match = SizeExpression.Match(normalized);
        if (!match.Success
            || !decimal.TryParse(match.Groups["n1"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return false;
        }

        var multiplier = ExpandUnit(match.Groups["u1"].Value) switch
        {
            "kb" => 1024L,
            "mb" => 1024L * 1024,
            "gb" => 1024L * 1024 * 1024,
            "tb" => 1024L * 1024 * 1024 * 1024,
            _ => 1L
        };

        var total = amount * multiplier;
        if (total > long.MaxValue)
        {
            return false;
        }

        bytes = (long)total;
        return true;
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

    internal static string ToDisplay(string value)
    {
        if (!TryNormalize(value, out var normalized))
        {
            return value;
        }

        return Pretty(normalized);
    }

    internal static bool IsNamedSize(string value) =>
        value.Equals("empty", StringComparison.OrdinalIgnoreCase)
        || value.Equals("tiny", StringComparison.OrdinalIgnoreCase)
        || value.Equals("small", StringComparison.OrdinalIgnoreCase)
        || value.Equals("medium", StringComparison.OrdinalIgnoreCase)
        || value.Equals("large", StringComparison.OrdinalIgnoreCase)
        || value.Equals("huge", StringComparison.OrdinalIgnoreCase)
        || value.Equals("gigantic", StringComparison.OrdinalIgnoreCase);

    private static bool TryNormalizeBoundPart(string input, out string part)
    {
        part = string.Empty;
        if (string.IsNullOrWhiteSpace(input)
            || !TryNormalize(input, out var normalized)
            || IsNamedSize(normalized)
            || normalized.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        if (normalized.StartsWith(">=", StringComparison.Ordinal) || normalized.StartsWith("<=", StringComparison.Ordinal))
        {
            part = normalized[2..];
        }
        else if (normalized[0] is '>' or '<')
        {
            part = normalized[1..];
        }
        else
        {
            part = normalized;
        }

        return part.Length > 0;
    }

    private static string FormatPart(string number, string unit)
    {
        if (decimal.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            && amount == decimal.Truncate(amount))
        {
            number = decimal.Truncate(amount).ToString(CultureInfo.InvariantCulture);
        }

        return number + ExpandUnit(unit);
    }

    private static string ExpandUnit(string unit) => unit.ToLowerInvariant() switch
    {
        "k" or "kb" => "kb",
        "m" or "mb" => "mb",
        "g" or "gb" => "gb",
        "t" or "tb" => "tb",
        "b" => "b",
        _ => string.Empty
    };

    private static string Pretty(string normalized) =>
        Regex.Replace(
            normalized,
            "kb|mb|gb|tb|b",
            match => match.Value.ToUpperInvariant(),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
