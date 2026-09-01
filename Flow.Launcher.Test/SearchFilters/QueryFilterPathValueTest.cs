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
    public void FormatToken_AlwaysQuotesAndAddsTrailingSlash()
    {
        Assert.That(QueryFilterPathValue.FormatToken(@"C:\Photos"), Is.EqualTo(@"path:""C:\Photos\"""));
        Assert.That(QueryFilterPathValue.FormatToken(@"C:\Program Files"), Is.EqualTo(@"path:""C:\Program Files\"""));
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
