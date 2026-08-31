namespace Flow.Launcher.Plugin;

/// <summary>
/// Describes how a result should be identified and replayed in recent-action history.
/// Plugin identity, query text, and execution time are captured by Flow Launcher.
/// </summary>
public sealed record HistoryActionDescriptor
{
    /// <summary>
    /// A stable, non-localized identifier that is unique within the plugin.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// A short, localized action label displayed in history.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// The semantic kind used to style the action consistently.
    /// </summary>
    public HistoryActionKind Kind { get; init; } = HistoryActionKind.Execute;

    /// <summary>
    /// Determines what selecting the saved history entry does.
    /// </summary>
    public HistoryReplayMode ReplayMode { get; init; } = HistoryReplayMode.Execute;
}

/// <summary>
/// Presentation kind for an action shown in history. Does not control replay;
/// use <see cref="HistoryReplayMode"/> for that.
/// </summary>
public enum HistoryActionKind
{
    /// <summary>
    /// The action kind is unspecified or could not be determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// Style as opening a file, folder, URL, or similar resource.
    /// </summary>
    Open,

    /// <summary>
    /// Style as a command or other non-destructive action.
    /// </summary>
    Execute,

    /// <summary>
    /// Style as a destructive action such as delete or kill.
    /// Does not prevent immediate replay; set <see cref="HistoryReplayMode.ShowQuery"/> to confirm first.
    /// </summary>
    Destructive
}

/// <summary>
/// Controls whether history executes a refreshed result or restores its query for confirmation.
/// </summary>
public enum HistoryReplayMode
{
    /// <summary>
    /// Re-runs the saved action immediately.
    /// </summary>
    Execute,

    /// <summary>
    /// Restores the original query so the user can confirm before executing.
    /// </summary>
    ShowQuery
}
