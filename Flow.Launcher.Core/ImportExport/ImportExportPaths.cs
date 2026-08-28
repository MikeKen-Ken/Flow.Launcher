using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Core.ImportExport;

public sealed class ImportExportPaths
{
    public string DataDirectory { get; init; } = string.Empty;

    public string SettingsDirectory { get; init; } = string.Empty;

    public string PluginsDirectory { get; init; } = string.Empty;

    public string ThemesDirectory { get; init; } = string.Empty;

    public static ImportExportPaths FromDataLocation() => new()
    {
        DataDirectory = DataLocation.DataDirectory(),
        SettingsDirectory = DataLocation.SettingsDirectory,
        PluginsDirectory = DataLocation.PluginsDirectory,
        ThemesDirectory = DataLocation.ThemesDirectory
    };
}
