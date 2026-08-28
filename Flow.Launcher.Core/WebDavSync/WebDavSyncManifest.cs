using System;

namespace Flow.Launcher.Core.WebDavSync;

public sealed class WebDavSyncManifest
{
    public int Version { get; set; } = 1;

    public DateTime ExportedAtUtc { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public bool IncludesSettings { get; set; }

    public bool IncludesPlugins { get; set; }
}
