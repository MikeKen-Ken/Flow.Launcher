using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace Flow.Launcher.Test;

public class PostBuildVersionScriptTest
{
    [TestCase("2.1.3.42", "2.1.3")]
    [TestCase("2.1.3-ci.42", "2.1.3-ci.42")]
    [TestCase("2.1.21", "2.1.21")]
    public void PrintsSemVerCompatiblePackageVersion(string sourceVersion, string expectedVersion)
    {
        var result = RunScript("flowVersion", sourceVersion);

        Assert.That(result.ExitCode, Is.EqualTo(0), result.Output);
        Assert.That(result.Output, Does.Contain($"Build Version: {expectedVersion}"));
    }

    [Test]
    public void PrefersExplicitPackageVersionOverAssemblyVersion()
    {
        var result = RunScript("flowPackageVersion", "2.1.3-ci.42", "flowVersion", "2.1.3.42");

        Assert.That(result.ExitCode, Is.EqualTo(0), result.Output);
        Assert.That(result.Output, Does.Contain("Build Version: 2.1.3-ci.42"));
    }

    [Test]
    public void RejectsInvalidPackageVersion()
    {
        var result = RunScript("flowVersion", "2.1");

        Assert.That(result.ExitCode, Is.Not.EqualTo(0));
        Assert.That(result.Output, Does.Contain("SemVer-compatible"));
    }

    private static (int ExitCode, string Output) RunScript(params string[] environmentPairs)
    {
        var scriptPath = Path.Combine(FindRepoRoot(), "Scripts", "post_build.ps1");
        var startInfo = new ProcessStartInfo
        {
            FileName = GetPowerShellPath(),
            Arguments = $"-NoProfile -File \"{scriptPath}\" -PrintPackageVersion",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment.Remove("flowVersion");
        startInfo.Environment.Remove("flowPackageVersion");
        for (var index = 0; index < environmentPairs.Length; index += 2)
        {
            startInfo.Environment[environmentPairs[index]] = environmentPairs[index + 1];
        }

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
        return File.Exists(systemPowerShell) ? systemPowerShell : "pwsh";
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Scripts", "post_build.ps1")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test directory.");
    }
}
