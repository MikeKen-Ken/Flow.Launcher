using System;

namespace Flow.Launcher.Core.ImportExport;

public sealed class ImportExportManifest
{
    public int Version { get; set; } = ImportExportConstants.ManifestVersion;

    public DateTime ExportedAtUtc { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public bool IncludesSettings { get; set; }

    public bool IncludesPlugins { get; set; }
}
