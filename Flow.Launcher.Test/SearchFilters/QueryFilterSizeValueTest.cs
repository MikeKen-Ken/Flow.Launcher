using Flow.Launcher.SearchFilters;
using NUnit.Framework;

namespace Flow.Launcher.Test.SearchFilters;

public class QueryFilterSizeValueTest
{
    [TestCase("1m", "1mb")]
    [TestCase("5M", "5mb")]
    [TestCase("1gb", "1gb")]
    [TestCase(">1m", ">1mb")]
    [TestCase("<10mb", "<10mb")]
    [TestCase("1m..5m", "1mb..5mb")]
    [TestCase("size:1gb", "1gb")]
    [TestCase("  > 2 gb ", ">2gb")]
    public void TryNormalize_ConcreteSizes_Succeeds(string input, string expected)
    {
        Assert.That(QueryFilterSizeValue.TryNormalize(input, out var normalized), Is.True);
        Assert.That(normalized, Is.EqualTo(expected));
    }

    [Test]
    public void Equals_TreatsShortAndLongUnitsAsSame()
    {
        Assert.That(QueryFilterSizeValue.Equals("5m", ">5mb"), Is.False);
        Assert.That(QueryFilterSizeValue.Equals("5m", "5mb"), Is.True);
        Assert.That(QueryFilterSizeValue.Equals(">1g", ">1gb"), Is.True);
    }

    [Test]
    public void Apply_NormalizesShortSizeUnits()
    {
        var result = QueryFilterSyntax.Apply("vacation", QueryFilterId.Size, "5m", QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo("vacation size:5mb"));
    }

    [Test]
    public void Apply_ReplacesSizeWithConcreteValue()
    {
        var result = QueryFilterSyntax.Apply("report size:small", QueryFilterId.Size, "1gb", QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo("report size:1gb"));
    }

    [Test]
    public void TryParseBounds_SplitsOperatorsAndRanges()
    {
        Assert.That(QueryFilterSizeValue.TryParseBounds(">20mb", out var min, out var max), Is.True);
        Assert.That(min, Is.EqualTo("20mb"));
        Assert.That(max, Is.EqualTo(string.Empty));

        Assert.That(QueryFilterSizeValue.TryParseBounds("<1gb", out min, out max), Is.True);
        Assert.That(min, Is.EqualTo(string.Empty));
        Assert.That(max, Is.EqualTo("1gb"));

        Assert.That(QueryFilterSizeValue.TryParseBounds("20mb..1gb", out min, out max), Is.True);
        Assert.That(min, Is.EqualTo("20mb"));
        Assert.That(max, Is.EqualTo("1gb"));
    }

    [Test]
    public void TryParseBounds_RejectsExactAndNamedSizes()
    {
        Assert.That(QueryFilterSizeValue.TryParseBounds("1gb", out _, out _), Is.False);
        Assert.That(QueryFilterSizeValue.TryParseBounds("small", out _, out _), Is.False);
    }

    [TestCase("20mb", "", ">20mb")]
    [TestCase("", "1gb", "<1gb")]
    [TestCase("20mb", "1gb", "20mb..1gb")]
    [TestCase("1gb", "20mb", "20mb..1gb")]
    [TestCase("20m", "1g", "20mb..1gb")]
    [TestCase("", "", "")]
    public void FormatBounds_CombinesMinAndMax(string min, string max, string expected)
    {
        Assert.That(QueryFilterSizeValue.FormatBounds(min, max), Is.EqualTo(expected));
    }

    [Test]
    public void Apply_CombinesGreaterAndLessIntoRange()
    {
        var result = QueryFilterSyntax.Apply("vacation", QueryFilterId.Size, "20mb..1gb", QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo("vacation size:20mb..1gb"));
    }

    [Test]
    public void SizeSteps_IndexOf_FindsExactAndNearest()
    {
        Assert.That(QueryFilterSizeSteps.IndexOf(""), Is.EqualTo(QueryFilterSizeSteps.AnyIndex));
        Assert.That(QueryFilterSizeSteps.TokenAt(QueryFilterSizeSteps.IndexOf("20mb")), Is.EqualTo("20mb"));
        Assert.That(QueryFilterSizeSteps.TokenAt(QueryFilterSizeSteps.IndexOf("1gb")), Is.EqualTo("1gb"));
        Assert.That(QueryFilterSizeSteps.TokenAt(QueryFilterSizeSteps.IndexOf("23mb")), Is.EqualTo("20mb"));
    }
}
