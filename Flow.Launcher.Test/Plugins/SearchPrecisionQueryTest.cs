using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.PrecisionMatching;
using NUnit.Framework;

namespace Flow.Launcher.Test.Plugins;

public class SearchPrecisionQueryTest
{
    [Test]
    public void Parse_RemovesControlsButKeepsProviderFilters()
    {
        var query = SearchPrecisionQuery.Parse("edge ext:exe match:exact case:");

        Assert.That(query.ProviderSearch, Is.EqualTo("edge ext:exe"));
        Assert.That(query.MatchText, Is.EqualTo("edge"));
        Assert.That(query.Mode, Is.EqualTo(NameMatchMode.Exact));
        Assert.That(query.CaseSensitive, Is.True);
        Assert.That(query.Extensions, Is.EqualTo(new[] { "exe" }));
    }

    [Test]
    public void Parse_UsesOnlyRecursivePathSearchBodyForName()
    {
        var query = SearchPrecisionQuery.Parse(@"C:\Program Files\>edge ext:exe exact:");

        Assert.That(query.ProviderSearch, Is.EqualTo(@"C:\Program Files\>edge ext:exe"));
        Assert.That(query.MatchText, Is.EqualTo("edge"));
    }

    [TestCase(@"C:\Program Files\Microsoft\Edge.exe", true)]
    [TestCase(@"C:\Program Files\Microsoft\msedge.exe", false)]
    [TestCase(@"C:\Program Files\Microsoft\edge-helper.exe", false)]
    public void Exact_WithExtensionFilter_MatchesFullComposedName(string path, bool expected)
    {
        var query = SearchPrecisionQuery.Parse("edge ext:exe match:exact");
        var result = new SearchResult { FullPath = path, Type = ResultType.File };

        Assert.That(SearchPrecisionMatcher.IsMatch(result, query), Is.EqualTo(expected));
    }

    [TestCase(@"C:\Apps\Edge.exe", true)]
    [TestCase(@"C:\Apps\edge.exe", false)]
    [TestCase(@"C:\Apps\Edge.EXE", false)]
    public void CaseSensitive_AppliesToTheNameTerm(string path, bool expected)
    {
        var query = SearchPrecisionQuery.Parse("Edge ext:exe case:");
        var result = new SearchResult { FullPath = path, Type = ResultType.File };

        Assert.That(SearchPrecisionMatcher.IsMatch(result, query), Is.EqualTo(expected));
    }

    [TestCase(@"C:\Apps\Studio-Visual.exe", true)]
    [TestCase(@"C:\Apps\studio-Visual.exe", false)]
    public void CaseSensitive_PreservesNormalAndTermSemantics(string path, bool expected)
    {
        var query = SearchPrecisionQuery.Parse("Visual Studio ext:exe case:");
        var result = new SearchResult { FullPath = path, Type = ResultType.File };

        Assert.That(SearchPrecisionMatcher.IsMatch(result, query), Is.EqualTo(expected));
    }

    [TestCase("prefix", @"C:\Apps\edge-helper.exe", true)]
    [TestCase("prefix", @"C:\Apps\my-edge.exe", false)]
    [TestCase("suffix", @"C:\Apps\my-edge.exe", true)]
    [TestCase("word", @"C:\Apps\new-edge-tool.exe", true)]
    [TestCase("word", @"C:\Apps\knowledge.exe", false)]
    public void NameModes_FilterTheFilenameStem(string mode, string path, bool expected)
    {
        var query = SearchPrecisionQuery.Parse($"edge ext:exe match:{mode}");
        var result = new SearchResult { FullPath = path, Type = ResultType.File };

        Assert.That(SearchPrecisionMatcher.IsMatch(result, query), Is.EqualTo(expected));
    }
}
