using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace Flow.Launcher.Test;

public class UpdatePluginVersionsScriptTest
{
    [Test]
    public void StampsPluginJsonWhenVersionIsProvided()
    {
        using var repo = new TempPluginRepo();
        repo.WritePluginJson("1.0.0");

        var result = RunScript(repo.Root, "-Version 2.1.3");

        Assert.That(result.ExitCode, Is.EqualTo(0), result.Output);
        Assert.That(repo.ReadPluginVersion(), Is.EqualTo("2.1.3"));
    }

    [Test]
    public void UsesVersionPrefixFromDotnetWorkflowWhenVersionIsOmitted()
    {
        using var repo = new TempPluginRepo();
        repo.WritePluginJson("1.0.0");
        repo.WriteDotnetWorkflow("2.1.3");

        var result = RunScript(repo.Root, string.Empty);

        Assert.That(result.ExitCode, Is.EqualTo(0), result.Output);
        Assert.That(repo.ReadPluginVersion(), Is.EqualTo("2.1.3"));
    }

    [Test]
    public void NormalizesFourPartFlowVersionToThreePartPluginVersion()
    {
        using var repo = new TempPluginRepo();
        repo.WritePluginJson("1.0.0");

        var result = RunScript(repo.Root, "-Version 2.1.3.88");

        Assert.That(result.ExitCode, Is.EqualTo(0), result.Output);
        Assert.That(repo.ReadPluginVersion(), Is.EqualTo("2.1.3"));
    }

    [Test]
    public void RefusesDevPlaceholderVersion()
    {
        using var repo = new TempPluginRepo();
        repo.WritePluginJson("1.0.0");

        var result = RunScript(repo.Root, "-Version 1.0.0");

        Assert.That(result.ExitCode, Is.Not.EqualTo(0), result.Output);
        Assert.That(result.Output, Does.Contain("1.0.0"));
        Assert.That(repo.ReadPluginVersion(), Is.EqualTo("1.0.0"));
    }

    [Test]
    public void ThrowsWhenNoVersionSourceIsAvailable()
    {
        using var repo = new TempPluginRepo();
        repo.WritePluginJson("1.0.0");

        var result = RunScript(repo.Root, string.Empty);

        Assert.That(result.ExitCode, Is.Not.EqualTo(0), result.Output);
        Assert.That(result.Output, Does.Contain("Unable to resolve"));
    }

    private static (int ExitCode, string Output) RunScript(string repoRoot, string extraArguments)
    {
        var scriptPath = Path.Combine(FindRepoRoot(), "Scripts", "update_plugin_versions.ps1");
        var arguments = $"-NoProfile -File \"{scriptPath}\" -RepoRoot \"{repoRoot}\" {extraArguments}";
        var startInfo = new ProcessStartInfo
        {
            FileName = GetPowerShellPath(),
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment.Remove("flowVersion");

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Failed to start PowerShell.");

        var output = new StringBuilder();
        output.Append(process.StandardOutput.ReadToEnd());
        output.Append(process.StandardError.ReadToEnd());
        process.WaitForExit();
        return (process.ExitCode, output.ToString());
    }

    private static string GetPowerShellPath()
    {
        var systemPowerShell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        if (File.Exists(systemPowerShell))
        {
            return systemPowerShell;
        }

        return "pwsh";
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            var scriptPath = Path.Combine(directory.FullName, "Scripts", "update_plugin_versions.ps1");
            if (File.Exists(scriptPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test directory.");
    }

    private sealed class TempPluginRepo : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "FlowLauncherPluginVersion-" + Guid.NewGuid().ToString("N"));

        public TempPluginRepo()
        {
            Directory.CreateDirectory(Path.Combine(Root, "Plugins", "FakePlugin"));
        }

        public void WritePluginJson(string version)
        {
            File.WriteAllText(
                Path.Combine(Root, "Plugins", "FakePlugin", "plugin.json"),
                $"{{\"Name\":\"Fake Plugin\",\"Version\":\"{version}\"}}");
        }

        public void WriteDotnetWorkflow(string versionPrefix)
        {
            var workflowDirectory = Path.Combine(Root, ".github", "workflows");
            Directory.CreateDirectory(workflowDirectory);
            File.WriteAllText(
                Path.Combine(workflowDirectory, "dotnet.yml"),
                $"name: Build{Environment.NewLine}jobs:{Environment.NewLine}  build:{Environment.NewLine}    env:{Environment.NewLine}      VersionPrefix: {versionPrefix}{Environment.NewLine}");
        }

        public string ReadPluginVersion()
        {
            using var document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(Root, "Plugins", "FakePlugin", "plugin.json")));
            return document.RootElement.GetProperty("Version").GetString();
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, true);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
