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
        var snapshot = QueryFilterSyntax.Parse("budget SIZE:Huge datemodified:yesterday files:");

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
    public void Parse_DoesNotTreatContentSearchKeywordAsExtension()
    {
        var snapshot = QueryFilterSyntax.Parse("doc: quarterly report");

        Assert.That(snapshot.IsActive(QueryFilterId.Extension), Is.False);
        Assert.That(snapshot.IsActive(QueryFilterId.File), Is.False);
    }

    [Test]
    public void Apply_PreservesNonFilterSearchTerms()
    {
        var result = QueryFilterSyntax.Apply("vacation 2024 folder:", QueryFilterId.Hidden, string.Empty, QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo("vacation 2024 folder: attrib:H"));
    }

    [Test]
    public void SetExtension_InsertsExtToken()
    {
        var result = QueryFilterSyntax.Apply("invoice", QueryFilterId.Extension, ".PDF", QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo("invoice ext:pdf"));
    }

    [Test]
    public void ToggleExtension_AddsSecondType()
    {
        var withPng = QueryFilterSyntax.Apply("photos", QueryFilterId.Extension, "png", QueryFilterApplyMode.Toggle);
        var withBoth = QueryFilterSyntax.Apply(withPng, QueryFilterId.Extension, "jpg", QueryFilterApplyMode.Toggle);

        Assert.That(withPng, Is.EqualTo("photos ext:png"));
        Assert.That(withBoth, Is.EqualTo("photos ext:png;jpg"));
    }

    [Test]
    public void ToggleExtension_RemovesOneTypeAndKeepsOthers()
    {
        var result = QueryFilterSyntax.Apply("photos ext:png;jpg;exe", QueryFilterId.Extension, "jpg", QueryFilterApplyMode.Toggle);

        Assert.That(result, Is.EqualTo("photos ext:png;exe"));
    }

    [Test]
    public void Parse_MergesSeparateExtTokens()
    {
        var snapshot = QueryFilterSyntax.Parse("notes ext:png ext:exe attrib:H da:today");

        Assert.That(snapshot.IsActive(QueryFilterId.Extension), Is.True);
        Assert.That(snapshot.GetValue(QueryFilterId.Extension), Is.EqualTo("png;exe"));
        Assert.That(snapshot.IsActive(QueryFilterId.Hidden), Is.True);
        Assert.That(snapshot.IsActive(QueryFilterId.DateAccessed), Is.True);
        Assert.That(snapshot.GetValue(QueryFilterId.DateAccessed), Is.EqualTo("today"));
    }

    [Test]
    public void ClearExtension_RemovesAllExtTokens()
    {
        var result = QueryFilterSyntax.Apply("photos ext:png;jpg", QueryFilterId.Extension, string.Empty, QueryFilterApplyMode.Clear);

        Assert.That(result, Is.EqualTo("photos"));
    }

    [Test]
    public void SetPath_InsertsRecursivePathCommand()
    {
        var result = QueryFilterSyntax.Apply("vacation", QueryFilterId.Path, @"C:\Photos", QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo(@"C:\Photos\>vacation"));
    }

    [Test]
    public void SetPath_KeepsSpacesInFolderPath()
    {
        var result = QueryFilterSyntax.Apply("report", QueryFilterId.Path, @"C:\Program Files", QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo(@"C:\Program Files\>report"));
    }

    [Test]
    public void Parse_RecognizesRecursivePathCommandWithSpaces()
    {
        var snapshot = QueryFilterSyntax.Parse(@"D:\Downloads\Flow-Launcher-Portable (1)\FlowLauncher\app-2.1.16\>notes ext:png");

        Assert.That(snapshot.IsActive(QueryFilterId.Path), Is.True);
        Assert.That(snapshot.GetValue(QueryFilterId.Path), Is.EqualTo(@"D:\Downloads\Flow-Launcher-Portable (1)\FlowLauncher\app-2.1.16"));
        Assert.That(snapshot.IsActive(QueryFilterId.Extension), Is.True);
        Assert.That(snapshot.GetValue(QueryFilterId.Extension), Is.EqualTo("png"));
    }

    [Test]
    public void Parse_StillRecognizesLegacyPathToken()
    {
        var snapshot = QueryFilterSyntax.Parse(@"notes path:""C:\Program Files"" ext:png");

        Assert.That(snapshot.IsActive(QueryFilterId.Path), Is.True);
        Assert.That(snapshot.GetValue(QueryFilterId.Path), Is.EqualTo(@"C:\Program Files"));
        Assert.That(snapshot.IsActive(QueryFilterId.Extension), Is.True);
    }

    [Test]
    public void SetPath_ReplacesExistingPathCommand()
    {
        var result = QueryFilterSyntax.Apply(@"C:\Old\>notes", QueryFilterId.Path, @"D:\New", QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo(@"D:\New\>notes"));
    }

    [Test]
    public void SetPath_RewritesLegacyPathTokenToCommand()
    {
        var result = QueryFilterSyntax.Apply(@"notes path:""C:\Old\""", QueryFilterId.Path, @"D:\New", QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo(@"D:\New\>notes"));
    }

    [Test]
    public void ClearPath_RemovesPathCommandAndKeepsFilters()
    {
        var result = QueryFilterSyntax.Apply(@"C:\Photos\>vacation ext:png", QueryFilterId.Path, string.Empty, QueryFilterApplyMode.Clear);

        Assert.That(result, Is.EqualTo("vacation ext:png"));
    }

    [Test]
    public void ApplyExtension_PreservesRecursivePathCommand()
    {
        var result = QueryFilterSyntax.Apply(@"C:\Program Files\>vacation", QueryFilterId.Extension, "jpg", QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo(@"C:\Program Files\>vacation ext:jpg"));
    }

    [Test]
    public void SetNameMatch_InsertsExactMode()
    {
        var result = QueryFilterSyntax.Apply("edge ext:exe", QueryFilterId.NameMatch, "exact", QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo("edge ext:exe match:exact"));
    }

    [Test]
    public void SetNameMatch_ReplacesPreviousMode()
    {
        var result = QueryFilterSyntax.Apply("edge match:prefix", QueryFilterId.NameMatch, "suffix", QueryFilterApplyMode.Set);

        Assert.That(result, Is.EqualTo("edge match:suffix"));
    }

    [Test]
    public void Parse_RecognizesPrecisionAliases()
    {
        var snapshot = QueryFilterSyntax.Parse("Edge ext:exe exact: case:");

        Assert.That(snapshot.GetValue(QueryFilterId.NameMatch), Is.EqualTo("exact"));
        Assert.That(snapshot.IsActive(QueryFilterId.CaseSensitive), Is.True);
    }

    [Test]
    public void Parse_DoesNotConsumeCaseModifierWithAttachedText()
    {
        var snapshot = QueryFilterSyntax.Parse("case:Edge");

        Assert.That(snapshot.IsActive(QueryFilterId.CaseSensitive), Is.False);
    }
}
