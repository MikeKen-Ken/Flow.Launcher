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
/// The semantic kind of an action shown in history.
/// </summary>
public enum HistoryActionKind
{
    Unknown,
    Open,
    Execute,
    Destructive
}

/// <summary>
/// Controls whether history executes a refreshed result or restores its query for confirmation.
/// </summary>
public enum HistoryReplayMode
{
    Execute,
    ShowQuery
}
