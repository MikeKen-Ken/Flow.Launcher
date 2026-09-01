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

    [Test]
    public void ToggleArchive_ReplacesImage()
    {
        var result = QueryFilterSyntax.Apply("backup type:image", QueryFilterId.Archive, string.Empty, QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo("backup type:archive"));
    }

    [Test]
    public void SetExtension_InsertsExtToken()
    {
        var result = QueryFilterSyntax.Apply("invoice", QueryFilterId.Extension, ".PDF", QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo("invoice ext:pdf"));
    }

    [Test]
    public void Parse_RecognizesArchiveExeHiddenAndAccessed()
    {
        var snapshot = QueryFilterSyntax.Parse("notes zip: attrib:H da:today");

        Assert.That(snapshot.IsActive(QueryFilterId.Archive), Is.True);
        Assert.That(snapshot.IsActive(QueryFilterId.Hidden), Is.True);
        Assert.That(snapshot.IsActive(QueryFilterId.DateAccessed), Is.True);
        Assert.That(snapshot.GetValue(QueryFilterId.DateAccessed), Is.EqualTo("today"));
    }

    [Test]
    public void SetPath_InsertsQuotedDirectoryToken()
    {
        var result = QueryFilterSyntax.Apply("vacation", QueryFilterId.Path, @"C:\Photos", QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo(@"vacation path:""C:\Photos\"""));
    }

    [Test]
    public void SetPath_QuotesFoldersWithSpaces()
    {
        var result = QueryFilterSyntax.Apply("report", QueryFilterId.Path, @"C:\Program Files", QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo(@"report path:""C:\Program Files\"""));
    }

    [Test]
    public void Parse_RecognizesQuotedPathWithSpaces()
    {
        var snapshot = QueryFilterSyntax.Parse(@"notes path:""C:\Program Files"" type:image");

        Assert.That(snapshot.IsActive(QueryFilterId.Path), Is.True);
        Assert.That(snapshot.GetValue(QueryFilterId.Path), Is.EqualTo(@"C:\Program Files"));
        Assert.That(snapshot.IsActive(QueryFilterId.Image), Is.True);
    }

    [Test]
    public void SetPath_ReplacesExistingPath()
    {
        var result = QueryFilterSyntax.Apply(@"notes path:""C:\Old\""", QueryFilterId.Path, @"D:\New", QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo(@"notes path:""D:\New\"""));
    }

    [Test]
    public void ClearPath_RemovesPathToken()
    {
        var result = QueryFilterSyntax.Apply(@"vacation path:""C:\Photos\"" type:image", QueryFilterId.Path, string.Empty, QueryFilterApplyMode.Clear);

        Assert.That(result, Is.EqualTo("vacation type:image"));
    }
}
