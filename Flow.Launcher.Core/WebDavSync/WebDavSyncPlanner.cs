using System;

namespace Flow.Launcher.Core.WebDavSync;

public static class WebDavSyncPlanner
{
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(2);

    public static WebDavSyncActionTaken Decide(
        DateTime? localMaxWriteUtc,
        DateTime? lastSuccessfulSyncUtc,
        DateTime? remoteExportedAtUtc)
    {
        if (!remoteExportedAtUtc.HasValue)
        {
            return WebDavSyncActionTaken.Uploaded;
        }

        // A device that has never synchronized must not overwrite an existing remote package
        // merely because saving its connection settings updated Settings.json.
        if (!lastSuccessfulSyncUtc.HasValue)
        {
            return WebDavSyncActionTaken.Downloaded;
        }

        var remote = remoteExportedAtUtc.Value;
        var localChanged = HasChangedSince(localMaxWriteUtc, lastSuccessfulSyncUtc);
        var remoteChanged = HasChangedSince(remote, lastSuccessfulSyncUtc);

        if (localChanged && !remoteChanged)
        {
            return WebDavSyncActionTaken.Uploaded;
        }

        if (remoteChanged && !localChanged)
        {
            return WebDavSyncActionTaken.Downloaded;
        }

        if (localChanged && remoteChanged)
        {
            if (localMaxWriteUtc.HasValue && localMaxWriteUtc.Value > remote + ClockSkew)
            {
                return WebDavSyncActionTaken.Uploaded;
            }

            if (!localMaxWriteUtc.HasValue || remote > localMaxWriteUtc.Value + ClockSkew)
            {
                return WebDavSyncActionTaken.Downloaded;
            }
        }

        return WebDavSyncActionTaken.AlreadyInSync;
    }

    private static bool HasChangedSince(DateTime? candidateUtc, DateTime? lastSuccessfulSyncUtc)
    {
        if (!lastSuccessfulSyncUtc.HasValue)
        {
            return true;
        }

        return candidateUtc.HasValue && candidateUtc.Value > lastSuccessfulSyncUtc.Value + ClockSkew;
    }
}
