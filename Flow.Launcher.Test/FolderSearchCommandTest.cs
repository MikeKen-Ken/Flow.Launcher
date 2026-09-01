using Flow.Launcher.Plugin.Explorer.Search.DirectoryInfo;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.ShellFolderSearch;
using NUnit.Framework;

namespace Flow.Launcher.Test;

[TestFixture]
public class FolderSearchCommandTest
{
    [Test]
    public void TryGetFolder_WhenSwitchAndPath_ReturnsNormalizedPath()
    {
        var found = FolderSearchCommand.TryGetFolder(
            ["Flow.Launcher.exe", "--search-folder", @"C:\Notes"],
            out var folderPath);

        Assert.That(found, Is.True);
        Assert.That(folderPath, Is.EqualTo(@"C:\Notes").IgnoreCase);
    }

    [Test]
    public void TryGetFolder_WhenEqualsForm_ReturnsNormalizedPath()
    {
        var found = FolderSearchCommand.TryGetFolder(
            ["Flow.Launcher.exe", "--search-folder=D:\\Projects"],
            out var folderPath);

        Assert.That(found, Is.True);
        Assert.That(folderPath, Is.EqualTo(@"D:\Projects").IgnoreCase);
    }

    [Test]
    public void TryGetFolder_WhenUncPath_ReturnsNormalizedPath()
    {
        var found = FolderSearchCommand.TryGetFolder(
            ["--search-folder", @"\\server\share\docs"],
            out var folderPath);

        Assert.That(found, Is.True);
        Assert.That(folderPath.TrimEnd('\\'), Is.EqualTo(@"\\server\share\docs").IgnoreCase);
    }

    [Test]
    public void TryGetFolder_WhenQuotedPath_StripsQuotes()
    {
        var found = FolderSearchCommand.TryGetFolder(
            ["Flow.Launcher.exe", "--search-folder", @"""C:\My Folder"""],
            out var folderPath);

        Assert.That(found, Is.True);
        Assert.That(folderPath, Is.EqualTo(@"C:\My Folder").IgnoreCase);
    }

    [Test]
    public void TryGetFolder_WhenSwitchMissing_ReturnsFalse()
    {
        var found = FolderSearchCommand.TryGetFolder(["Flow.Launcher.exe"], out var folderPath);

        Assert.That(found, Is.False);
        Assert.That(folderPath, Is.Null);
    }

    [Test]
    public void TryGetFolder_WhenSwitchHasNoValue_ReturnsFalse()
    {
        var found = FolderSearchCommand.TryGetFolder(["--search-folder"], out var folderPath);

        Assert.That(found, Is.False);
        Assert.That(folderPath, Is.Null);
    }

    [Test]
    public void TryGetFolder_WhenPathIsRelative_ReturnsFalse()
    {
        var found = FolderSearchCommand.TryGetFolder(["--search-folder", @"notes\inbox"], out _);

        Assert.That(found, Is.False);
    }

    [TestCase(@"C:\Notes", @"C:\Notes\>")]
    [TestCase(@"C:\Notes\", @"C:\Notes\>")]
    [TestCase(@"C:\", @"C:\>")]
    [TestCase(@"C:", @"C:\>")]
    [TestCase(@"D:/Projects/", @"D:\Projects\>")]
    [TestCase(@"\\server\share\docs", @"\\server\share\docs\>")]
    public void BuildQuery_AppendsRecursiveSearchIndicator(string folderPath, string expectedQuery)
    {
        var query = FolderSearchCommand.BuildQuery(folderPath);

        Assert.That(query, Is.EqualTo(expectedQuery).IgnoreCase);
    }

    [TestCase(@"C:\Notes", @"C:\Notes\")]
    [TestCase(@"D:\Projects", @"D:\Projects\")]
    [TestCase(@"\\server\share\docs", @"\\server\share\docs\")]
    public void BuildQuery_UsesSelectedFolderAsExplorerSearchRoot(string folderPath, string expectedRoot)
    {
        var query = FolderSearchCommand.BuildQuery(folderPath);

        Assert.That(
            FilesFolders.ReturnPreviousDirectoryIfIncompleteString(query),
            Is.EqualTo(expectedRoot).IgnoreCase);
        Assert.That(DirectoryInfoSearch.ConstructSearchCriteria(query), Is.EqualTo("**"));
    }

    [Test]
    public void BuildCommandLine_QuotesExecutableAndPlaceholder()
    {
        var command = FolderSearchCommand.BuildCommandLine(@"C:\Program Files\Flow.Launcher\Flow.Launcher.exe", "%1");

        Assert.That(command, Is.EqualTo(
            @"""C:\Program Files\Flow.Launcher\Flow.Launcher.exe"" --search-folder ""%1"""));
    }

    [Test]
    public void BuildQuery_WhenPathInvalid_ReturnsEmpty()
    {
        Assert.That(FolderSearchCommand.BuildQuery(""), Is.Empty);
        Assert.That(FolderSearchCommand.BuildQuery("relative"), Is.Empty);
        Assert.That(FolderSearchCommand.BuildQuery("C:\\invalid\0path"), Is.Empty);
    }

    [Test]
    public void ExplorerFolderContextMenu_DefaultsToEnabled()
    {
        var settings = new Flow.Launcher.Infrastructure.UserSettings.Settings();

        Assert.That(settings.EnableExplorerFolderContextMenu, Is.True);
    }

    [Test]
    public void HandleLifecycleCommand_WhenFolderSearch_DoesNotHandleCommand()
    {
        var handled = ShellFolderSearchMenu.HandleLifecycleCommand(
            ["Flow.Launcher.exe", "--search-folder", @"C:\Notes"],
            out var error);

        Assert.That(handled, Is.False);
        Assert.That(error, Is.Null);
    }
}
