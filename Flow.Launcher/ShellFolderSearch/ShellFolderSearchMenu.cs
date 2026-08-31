using System;
using System.Collections.Generic;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Microsoft.Win32;

namespace Flow.Launcher.ShellFolderSearch;

/// <summary>
/// Registers a per-user Windows Explorer context menu item that launches Flow Launcher
/// scoped to the selected folder (or the folder whose background was clicked).
/// </summary>
public static class ShellFolderSearchMenu
{
    internal const string ShellKeyName = "FlowLauncherSearchFolder";
    private const string SquirrelUninstallArgument = "--squirrel-uninstall";

    public static bool IsSupported => !DataLocation.PortableDataLocationInUse();

    private static readonly ShellRegistration[] Registrations =
    [
        new(@"Software\Classes\Directory\shell", "%1"),
        new(@"Software\Classes\Directory\Background\shell", "%V"),
        new(@"Software\Classes\Drive\shell", "%1")
    ];

    public static void Sync(bool enabled, string menuText)
    {
        if (enabled)
            Register(menuText);
        else
            Unregister();
    }

    public static void Register(string menuText)
    {
        if (!IsSupported)
            throw new InvalidOperationException("Explorer folder search registration is unavailable in portable mode.");

        if (string.IsNullOrWhiteSpace(menuText))
            throw new ArgumentException("Menu text is required.", nameof(menuText));

        var exePath = Constant.ExecutablePath;
        try
        {
            foreach (var registration in Registrations)
            {
                WriteShellKey(registration.ShellParentPath, menuText, exePath, registration.PathPlaceholder);
            }
        }
        catch (Exception registrationException)
        {
            try
            {
                Unregister();
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    "Explorer folder search registration failed and could not be fully rolled back.",
                    registrationException,
                    rollbackException);
            }

            throw;
        }
    }

    public static void Unregister()
    {
        List<Exception> errors = [];
        foreach (var registration in Registrations)
        {
            try
            {
                DeleteShellKey(registration.ShellParentPath);
            }
            catch (Exception e)
            {
                errors.Add(e);
            }
        }

        if (errors.Count > 0)
            throw new AggregateException("Unable to remove all Explorer folder search registrations.", errors);
    }

    public static bool HandleLifecycleCommand(string[] args, out Exception error)
    {
        error = null;
        if (!ContainsArgument(args, SquirrelUninstallArgument))
            return false;

        try
        {
            Unregister();
        }
        catch (Exception e)
        {
            error = e;
        }

        return true;
    }

    private static void WriteShellKey(string shellParentPath, string menuText, string exePath, string pathPlaceholder)
    {
        using var shellKey = Registry.CurrentUser.CreateSubKey(shellParentPath + "\\" + ShellKeyName, writable: true)
                             ?? throw new InvalidOperationException($"Unable to create registry key {shellParentPath}\\{ShellKeyName}");

        shellKey.SetValue(null, menuText);
        shellKey.SetValue("MUIVerb", menuText);
        shellKey.SetValue("Icon", exePath);
        shellKey.SetValue("Position", "Top");
        shellKey.SetValue("NeverDefault", string.Empty);
        shellKey.SetValue("MultiSelectModel", "Single");

        using var commandKey = shellKey.CreateSubKey("command", writable: true)
                               ?? throw new InvalidOperationException($"Unable to create command key under {shellParentPath}\\{ShellKeyName}");

        commandKey.SetValue(null, FolderSearchCommand.BuildCommandLine(exePath, pathPlaceholder));
    }

    private static void DeleteShellKey(string shellParentPath)
    {
        using var parent = Registry.CurrentUser.OpenSubKey(shellParentPath, writable: true);
        parent?.DeleteSubKeyTree(ShellKeyName, throwOnMissingSubKey: false);
    }

    private static bool ContainsArgument(string[] args, string expected)
    {
        if (args is null)
            return false;

        foreach (var arg in args)
        {
            if (string.Equals(arg, expected, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private readonly record struct ShellRegistration(string ShellParentPath, string PathPlaceholder);
}
