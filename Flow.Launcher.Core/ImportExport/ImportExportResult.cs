namespace Flow.Launcher.Core.ImportExport;

public sealed class ImportExportResult
{
    public bool Success { get; init; }

    public bool RequiresRestart { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;

    public static ImportExportResult Failed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };

    public static ImportExportResult Ok(bool requiresRestart = false) => new()
    {
        Success = true,
        RequiresRestart = requiresRestart
    };
}
