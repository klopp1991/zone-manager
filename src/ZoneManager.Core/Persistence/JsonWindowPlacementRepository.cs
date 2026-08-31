using System.Text.Json;
using ZoneManager.Core.Placement;

namespace ZoneManager.Core.Persistence;

public sealed class JsonWindowPlacementRepository : IWindowPlacementRepository
{
    private const string PrimaryFileName = "placements.json";
    private const string BackupFileName = "placements.backup-1.json";
    private const int MaximumEntries = 500;

    private readonly string directoryPath;
    private readonly JsonSerializerOptions serializerOptions;

    public JsonWindowPlacementRepository(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        this.directoryPath = directoryPath;
        serializerOptions = JsonConfigurationRepository.CreateSerializerOptions();
    }

    public async Task<WindowPlacementLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        var primaryPath = Path.Combine(directoryPath, PrimaryFileName);
        if (!File.Exists(primaryPath))
        {
            var recovered = await RecoverFromBackupAsync(cancellationToken);
            return recovered is null
                ? new WindowPlacementLoadResult(WindowPlacementCatalog.Empty, false)
                : new WindowPlacementLoadResult(recovered, true, "Die Platzierungen wurden aus der Sicherung wiederhergestellt.");
        }

        try
        {
            return new WindowPlacementLoadResult(await ReadCatalogAsync(primaryPath, cancellationToken), false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            Directory.CreateDirectory(directoryPath);
            var invalidFileName = $"placements.invalid-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json";
            File.Move(primaryPath, Path.Combine(directoryPath, invalidFileName));

            var recovered = await RecoverFromBackupAsync(cancellationToken);
            if (recovered is not null)
            {
                return new WindowPlacementLoadResult(
                    recovered,
                    true,
                    $"Die Platzierungen waren ungültig, wurden als {invalidFileName} gesichert und aus der Sicherung wiederhergestellt.");
            }

            return new WindowPlacementLoadResult(
                WindowPlacementCatalog.Empty,
                true,
                $"Die Platzierungen waren ungültig und wurden als {invalidFileName} gesichert.");
        }
    }

    public async Task SaveAsync(WindowPlacementCatalog catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Validate(catalog);
        Directory.CreateDirectory(directoryPath);

        var retainedCatalog = new WindowPlacementCatalog(
            WindowPlacementCatalog.CurrentSchemaVersion,
            catalog.Entries
                .OrderByDescending(entry => entry.LastUpdatedUtc)
                .GroupBy(entry => entry.Identity)
                .Select(group => group.First())
                .Take(MaximumEntries)
                .ToArray());
        var primaryPath = Path.Combine(directoryPath, PrimaryFileName);
        var backupPath = Path.Combine(directoryPath, BackupFileName);
        var temporaryPath = Path.Combine(directoryPath, $"placements.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, retainedCatalog, serializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (File.Exists(primaryPath))
            {
                File.Replace(temporaryPath, primaryPath, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, primaryPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private async Task<WindowPlacementCatalog> ReadCatalogAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var catalog = await JsonSerializer.DeserializeAsync<WindowPlacementCatalog>(stream, serializerOptions, cancellationToken);
        Validate(catalog);
        return catalog!;
    }

    private async Task<WindowPlacementCatalog?> RecoverFromBackupAsync(CancellationToken cancellationToken)
    {
        var backupPath = Path.Combine(directoryPath, BackupFileName);
        if (!File.Exists(backupPath))
        {
            return null;
        }

        try
        {
            var backup = await ReadCatalogAsync(backupPath, cancellationToken);
            await SaveAsync(backup, cancellationToken);
            return backup;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            // Die ungültige Sicherung wird nicht als neuer Primärspeicher übernommen.
            return null;
        }
    }

    private static void Validate(WindowPlacementCatalog? catalog)
    {
        if (catalog is null)
        {
            throw new InvalidDataException("Die Platzierungen sind leer.");
        }

        if (catalog.SchemaVersion != WindowPlacementCatalog.CurrentSchemaVersion)
        {
            throw new InvalidDataException("Die Platzierungsversion wird nicht unterstützt.");
        }

        if (catalog.Entries is null)
        {
            throw new InvalidDataException("Die Platzierungseinträge fehlen.");
        }

        foreach (var entry in catalog.Entries)
        {
            if (entry is null)
            {
                throw new InvalidDataException("Fensterplatzierungen dürfen keine leeren Einträge enthalten.");
            }

            if (entry.Identity is null ||
                string.IsNullOrWhiteSpace(entry.Identity.ApplicationKey) ||
                string.IsNullOrWhiteSpace(entry.Identity.WindowClass) ||
                !Enum.IsDefined(entry.Identity.Kind))
            {
                throw new InvalidDataException("Eine Fensterplatzierung enthält eine ungültige Fensteridentität.");
            }

            if (string.IsNullOrWhiteSpace(entry.MonitorStableId) ||
                entry.SourceWorkArea.Width <= 0 ||
                entry.SourceWorkArea.Height <= 0 ||
                entry.NormalBoundsPixels.Width <= 0 ||
                entry.NormalBoundsPixels.Height <= 0 ||
                entry.NormalBoundsNormalized is null ||
                !double.IsFinite(entry.NormalBoundsNormalized.X) ||
                !double.IsFinite(entry.NormalBoundsNormalized.Y) ||
                !double.IsFinite(entry.NormalBoundsNormalized.Width) ||
                !double.IsFinite(entry.NormalBoundsNormalized.Height) ||
                entry.NormalBoundsNormalized.Width <= 0 ||
                entry.NormalBoundsNormalized.Height <= 0)
            {
                throw new InvalidDataException("Eine Fensterplatzierung enthält ungültige Geometriedaten.");
            }
        }
    }
}
