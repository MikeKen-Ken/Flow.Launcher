namespace Flow.Launcher.Core.WebDavSync;

public sealed class WebDavConnection
{
    public string Url { get; init; } = string.Empty;

    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
