using System;

namespace Flow.Launcher.Core.WebDavSync;

public sealed class WebDavRemoteFileInfo
{
    public bool Exists { get; init; }

    public DateTime? LastModifiedUtc { get; init; }

    public long? Length { get; init; }

    public static WebDavRemoteFileInfo Missing { get; } = new() { Exists = false };
}
