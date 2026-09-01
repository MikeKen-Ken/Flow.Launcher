using Flow.Launcher.SearchFilters;
using NUnit.Framework;

namespace Flow.Launcher.Test.SearchFilters;

public class QueryFilterExtensionValueTest
{
    [Test]
    public void Parse_SplitsEverythingSeparators()
    {
        var parsed = QueryFilterExtensionValue.Parse("PNG;jpg|gif,webp");

        Assert.That(parsed, Is.EqualTo(new[] { "png", "jpg", "gif", "webp" }));
    }

    [Test]
    public void Toggle_AddsAndRemoves()
    {
        var withPng = QueryFilterExtensionValue.Toggle([], "png");
        var withBoth = QueryFilterExtensionValue.Toggle(withPng, ".JPG");
        var withoutJpg = QueryFilterExtensionValue.Toggle(withBoth, "jpg");

        Assert.That(QueryFilterExtensionValue.Join(withPng), Is.EqualTo("png"));
        Assert.That(QueryFilterExtensionValue.Join(withBoth), Is.EqualTo("png;jpg"));
        Assert.That(QueryFilterExtensionValue.Join(withoutJpg), Is.EqualTo("png"));
    }

    [Test]
    public void Join_FollowsPresetOrder()
    {
        var joined = QueryFilterExtensionValue.Join(["exe", "png", "pdf"]);

        Assert.That(joined, Is.EqualTo("png;pdf;exe"));
    }

    [Test]
    public void ToDisplay_UsesCommaSeparatedList()
    {
        Assert.That(QueryFilterExtensionValue.ToDisplay("png;jpg"), Is.EqualTo("png, jpg"));
    }
}
