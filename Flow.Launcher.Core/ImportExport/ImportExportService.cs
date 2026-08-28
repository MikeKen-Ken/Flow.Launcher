using System;
using System.Threading;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Core.ImportExport;

public sealed class ImportExportService
{
    private static readonly string ClassName = nameof(ImportExportService);

    private readonly ImportExportPaths _paths;

    public ImportExportService() : this(ImportExportPaths.FromDataLocation())
    {
    }

    public ImportExportService(ImportExportPaths paths)
    {
        _paths = paths;
    }

    public ImportExportResult ExportToFolder(
        string destinationDirectory,
        bool includeSettings,
        bool includePlugins,
        Action<double> reportProgress = null,
        CancellationToken token = default)
    {
        return Run(
            () =>
            {
                ValidateSelection(includeSettings, includePlugins);
                ValidateDestinationDirectory(destinationDirectory);
                token.ThrowIfCancellationRequested();
                reportProgress?.Invoke(20);
                ImportExportPackage.WriteToDirectory(
                    destinationDirectory, _paths, includeSettings, includePlugins, token);
                reportProgress?.Invoke(100);
                return ImportExportResult.Ok();
            });
    }

    public ImportExportResult ExportToZip(
        string zipPath,
        bool includeSettings,
        bool includePlugins,
        Action<double> reportProgress = null,
        CancellationToken token = default)
    {
        return Run(
            () =>
            {
                ValidateSelection(includeSettings, includePlugins);
                if (string.IsNullOrWhiteSpace(zipPath))
                {
                    throw new InvalidOperationException(Localize.importExportInvalidPackage());
                }

                token.ThrowIfCancellationRequested();
                reportProgress?.Invoke(20);
                ImportExportPackage.CreateZip(zipPath, _paths, includeSettings, includePlugins, token);
                reportProgress?.Invoke(100);
                return ImportExportResult.Ok();
            });
    }

    public ImportExportResult ImportFromFolder(
        string sourceDirectory,
        bool applySettings,
        bool applyPlugins,
        Action<double> reportProgress = null,
        CancellationToken token = default)
    {
        return Run(
            () =>
            {
                ValidateSelection(applySettings, applyPlugins);
                if (!ImportExportPackage.IsPackageDirectory(sourceDirectory))
                {
                    return ImportExportResult.Failed(Localize.importExportInvalidPackage());
                }

                var manifest = ImportExportPackage.ReadManifestFromDirectory(sourceDirectory);
                var resolvedSettings = applySettings && manifest.IncludesSettings;
                var resolvedPlugins = applyPlugins && manifest.IncludesPlugins;
                if (!resolvedSettings && !resolvedPlugins)
                {
                    return ImportExportResult.Failed(Localize.importExportMissingContent());
                }

                token.ThrowIfCancellationRequested();
                reportProgress?.Invoke(40);
                ImportExportPendingApply.StageDirectory(
                    sourceDirectory, _paths.DataDirectory, resolvedSettings, resolvedPlugins);
                reportProgress?.Invoke(100);
                return ImportExportResult.Ok(requiresRestart: true);
            });
    }

    public ImportExportResult ImportFromZip(
        string zipPath,
        bool applySettings,
        bool applyPlugins,
        Action<double> reportProgress = null,
        CancellationToken token = default)
    {
        return Run(
            () =>
            {
                ValidateSelection(applySettings, applyPlugins);
                if (!ImportExportPackage.IsPackageZip(zipPath))
                {
                    return ImportExportResult.Failed(Localize.importExportInvalidPackage());
                }

                var manifest = ImportExportPackage.ReadManifestFromZip(zipPath);
                var resolvedSettings = applySettings && manifest.IncludesSettings;
                var resolvedPlugins = applyPlugins && manifest.IncludesPlugins;
                if (!resolvedSettings && !resolvedPlugins)
                {
                    return ImportExportResult.Failed(Localize.importExportMissingContent());
                }

                token.ThrowIfCancellationRequested();
                reportProgress?.Invoke(40);
                ImportExportPendingApply.StageZip(zipPath, _paths.DataDirectory, resolvedSettings, resolvedPlugins);
                reportProgress?.Invoke(100);
                return ImportExportResult.Ok(requiresRestart: true);
            });
    }

    private ImportExportResult Run(Func<ImportExportResult> operation)
    {
        try
        {
            return operation();
        }
        catch (OperationCanceledException)
        {
            return ImportExportResult.Failed(Localize.importExportCancelled());
        }
        catch (Exception e)
        {
            Log.Info(ClassName, $"Import/export failed: {e.Message}");
            return ImportExportResult.Failed(e.Message);
        }
    }

    private static void ValidateSelection(bool includeSettings, bool includePlugins)
    {
        if (!includeSettings && !includePlugins)
        {
            throw new InvalidOperationException(Localize.importExportNothingSelected());
        }
    }

    private void ValidateDestinationDirectory(string destinationDirectory)
    {
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new InvalidOperationException(Localize.importExportInvalidPackage());
        }

        if (ImportExportPackage.OverlapsDataPaths(destinationDirectory, _paths))
        {
            throw new InvalidOperationException(Localize.importExportUnsafeDestination());
        }
    }
}
