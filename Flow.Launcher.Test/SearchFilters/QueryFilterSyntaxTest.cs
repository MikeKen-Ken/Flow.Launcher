using Flow.Launcher.SearchFilters;
using NUnit.Framework;

namespace Flow.Launcher.Test.SearchFilters;

public class QueryFilterSyntaxTest
{
    [Test]
    public void ToggleFile_OnEmptyQuery_InsertsFileToken()
    {
        var result = QueryFilterSyntax.Apply(string.Empty, QueryFilterId.File, string.Empty, QueryFilterApplyMode.Toggle);

        Assert.That(result, Is.EqualTo("file:"));
    }

    [Test]
    public void ToggleFile_WhenAlreadyActive_RemovesFileToken()
    {
        var result = QueryFilterSyntax.Apply("vacation file:", QueryFilterId.File, string.Empty, QueryFilterApplyMode.Toggle);

        Assert.That(result, Is.EqualTo("vacation"));
    }

    [Test]
    public void ToggleFile_WhenFolderActive_ReplacesFolder()
    {
        var result = QueryFilterSyntax.Apply("vacation folder:", QueryFilterId.File, string.Empty, QueryFilterApplyMode.Toggle);

        Assert.That(result, Is.EqualTo("vacation file:"));
    }

    [Test]
    public void ToggleImage_ReplacesVideo()
    {
        var result = QueryFilterSyntax.Apply("cat type:video", QueryFilterId.Image, string.Empty, QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo("cat type:image"));
    }

    [Test]
    public void SetSize_ReplacesExistingSize()
    {
        var result = QueryFilterSyntax.Apply("report size:small", QueryFilterId.Size, "large", QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo("report size:large"));
    }

    [Test]
    public void ToggleSameSize_ClearsFilter()
    {
        var result = QueryFilterSyntax.Apply("report size:large", QueryFilterId.Size, "large", QueryFilterApplyMode.Toggle);

        Assert.That(result, Is.EqualTo("report"));
    }

    [Test]
    public void DateModifiedAndCreated_AreIndependent()
    {
        var withModified = QueryFilterSyntax.Apply("notes", QueryFilterId.DateModified, "today", QueryFilterApplyMode.Set);
        var withBoth = QueryFilterSyntax.Apply(withModified, QueryFilterId.DateCreated, "thisyear", QueryFilterApplyMode.Set);

        Assert.That(withBoth, Is.EqualTo("notes dm:today dc:thisyear"));
    }

    [Test]
    public void Parse_RecognizesAliases()
    {
        var snapshot = QueryFilterSyntax.Parse("budget pic: SIZE:Huge datemodified:yesterday files:");

        Assert.That(snapshot.IsActive(QueryFilterId.Image), Is.True);
        Assert.That(snapshot.IsActive(QueryFilterId.Size), Is.True);
        Assert.That(snapshot.GetValue(QueryFilterId.Size), Is.EqualTo("Huge"));
        Assert.That(snapshot.IsActive(QueryFilterId.DateModified), Is.True);
        Assert.That(snapshot.GetValue(QueryFilterId.DateModified), Is.EqualTo("yesterday"));
        Assert.That(snapshot.IsActive(QueryFilterId.File), Is.True);
    }

    [Test]
    public void Parse_DoesNotTreatFileNameAsFilter()
    {
        var snapshot = QueryFilterSyntax.Parse("file.txt folder-name");

        Assert.That(snapshot.IsActive(QueryFilterId.File), Is.False);
        Assert.That(snapshot.IsActive(QueryFilterId.Folder), Is.False);
    }

    [Test]
    public void Parse_DoesNotTreatContentSearchKeywordAsDocumentFilter()
    {
        var snapshot = QueryFilterSyntax.Parse("doc: quarterly report");

        Assert.That(snapshot.IsActive(QueryFilterId.Document), Is.False);
    }

    [Test]
    public void Apply_PreservesNonFilterSearchTerms()
    {
        var result = QueryFilterSyntax.Apply("vacation 2024 folder:", QueryFilterId.Image, string.Empty, QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo("vacation 2024 folder: type:image"));
    }
}
