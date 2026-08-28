using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Core.WebDavSync;

public sealed class WebDavSyncPaths
{
    public string DataDirectory { get; init; } = string.Empty;

    public string SettingsDirectory { get; init; } = string.Empty;

    public string PluginsDirectory { get; init; } = string.Empty;

    public string ThemesDirectory { get; init; } = string.Empty;

    public static WebDavSyncPaths FromDataLocation() => new()
    {
        DataDirectory = DataLocation.DataDirectory(),
        SettingsDirectory = DataLocation.SettingsDirectory,
        PluginsDirectory = DataLocation.PluginsDirectory,
        ThemesDirectory = DataLocation.ThemesDirectory
    };
}
