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
}
