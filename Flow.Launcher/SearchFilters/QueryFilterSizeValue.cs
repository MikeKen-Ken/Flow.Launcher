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
