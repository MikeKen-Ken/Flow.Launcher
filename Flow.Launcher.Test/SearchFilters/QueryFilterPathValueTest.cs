using Flow.Launcher.SearchFilters;
using NUnit.Framework;

namespace Flow.Launcher.Test.SearchFilters;

public class QueryFilterPathValueTest
{
    [TestCase(@"C:\Photos", @"C:\Photos")]
    [TestCase(@"C:\Photos\", @"C:\Photos")]
    [TestCase(@"""C:\Program Files""", @"C:\Program Files")]
    [TestCase(@"path:""C:\Program Files\""", @"C:\Program Files")]
    [TestCase(@"C:/Photos/", @"C:\Photos")]
    [TestCase(@"C:\", @"C:\")]
    public void TryNormalize_WindowsPaths_Succeeds(string input, string expected)
    {
        Assert.That(QueryFilterPathValue.TryNormalize(input, out var path), Is.True);
        Assert.That(path, Is.EqualTo(expected));
    }

    [Test]
    public void FormatCommand_UsesRecursivePathSearchSyntax()
    {
        Assert.That(QueryFilterPathValue.FormatCommand(@"C:\Photos"), Is.EqualTo(@"C:\Photos\>"));
        Assert.That(QueryFilterPathValue.FormatCommand(@"C:\Program Files"), Is.EqualTo(@"C:\Program Files\>"));
        Assert.That(
            QueryFilterPathValue.FormatCommand(@"D:\Downloads\Flow-Launcher-Portable (1)\FlowLauncher\app-2.1.16"),
            Is.EqualTo(@"D:\Downloads\Flow-Launcher-Portable (1)\FlowLauncher\app-2.1.16\>"));
    }

    [Test]
    public void TrySplitScope_ReadsRecursivePathCommand()
    {
        var query = @"D:\Downloads\Flow-Launcher-Portable (1)\FlowLauncher\app-2.1.16\>vacation type:image";

        Assert.That(QueryFilterPathValue.TrySplitScope(query, out var path, out var remainder), Is.True);
        Assert.That(path, Is.EqualTo(@"D:\Downloads\Flow-Launcher-Portable (1)\FlowLauncher\app-2.1.16"));
        Assert.That(remainder, Is.EqualTo("vacation type:image"));
    }

    [Test]
    public void ToDisplay_UsesFolderName()
    {
        Assert.That(QueryFilterPathValue.ToDisplay(@"C:\Photos"), Is.EqualTo("Photos"));
        Assert.That(QueryFilterPathValue.ToDisplay(@"C:\"), Is.EqualTo(@"C:\"));
    }

    [Test]
    public void Equals_IgnoresTrailingSlashAndQuotes()
    {
        Assert.That(QueryFilterPathValue.Equals(@"C:\Photos\", @"""C:\Photos"""), Is.True);
        Assert.That(QueryFilterPathValue.Equals(@"C:\Photos", @"D:\Photos"), Is.False);
    }
}
