namespace Flow.Launcher.Core.WebDavSync;

public sealed class WebDavSyncResult
{
    public bool Success { get; init; }

    public bool RequiresRestart { get; init; }

    public WebDavSyncActionTaken ActionTaken { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;

    public static WebDavSyncResult Failed(string errorMessage) => new()
    {
        Success = false,
        ActionTaken = WebDavSyncActionTaken.None,
        ErrorMessage = errorMessage
    };

    public static WebDavSyncResult Ok(WebDavSyncActionTaken actionTaken, bool requiresRestart = false) => new()
    {
        Success = true,
        ActionTaken = actionTaken,
        RequiresRestart = requiresRestart
    };
}
