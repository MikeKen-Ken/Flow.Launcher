using Flow.Launcher.SearchFilters;
using NUnit.Framework;

namespace Flow.Launcher.Test.SearchFilters;

public class QueryFilterExtensionValueTest
{
    [TestCase(".APPREF-MS", "appref-ms")]
    [TestCase(".lnk", "lnk")]
    [TestCase("log", "log")]
    public void Normalize_AcceptsCustomAndShortcutExtensions(string input, string expected)
    {
        Assert.That(QueryFilterExtensionValue.TryNormalizeOne(input, out var result), Is.True);
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("-lnk")]
    [TestCase("lnk-")]
    [TestCase("*")]
    [TestCase("log ext:exe")]
    [TestCase("log;exe")]
    [TestCase("../lnk")]
    [TestCase("abcdefghijklm")]
    public void Normalize_RejectsInvalidSingleExtensions(string input)
    {
        Assert.That(QueryFilterExtensionValue.TryNormalizeOne(input, out _), Is.False);
    }

    [Test]
    public void CustomAddition_PreservesExistingSelectionAndRoundTrips()
    {
        var merged = QueryFilterExtensionValue.Join(["png;lnk;log", ".LOG", "appref-ms"]);
        var query = QueryFilterSyntax.Apply("notes ext:png;lnk;log", QueryFilterId.Extension,
            merged, QueryFilterApplyMode.Set);
        var parsed = QueryFilterSyntax.Parse(query).GetValue(QueryFilterId.Extension);
        Assert.That(QueryFilterExtensionValue.Parse(parsed),
            Is.EquivalentTo(new[] { "png", "lnk", "log", "appref-ms" }));
        Assert.That(QueryFilterExtensionValue.Toggle(QueryFilterExtensionValue.Parse(parsed), "log"),
            Is.EquivalentTo(new[] { "png", "lnk", "appref-ms" }));
    }

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
