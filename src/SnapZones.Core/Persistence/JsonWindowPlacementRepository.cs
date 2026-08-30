using System.Text.Json;
using SnapZones.Core.Placement;

namespace SnapZones.Core.Persistence;

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
            return new WindowPlacementLoadResult(WindowPlacementCatalog.Empty, false);
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

            var backupPath = Path.Combine(directoryPath, BackupFileName);
            try
            {
                if (File.Exists(backupPath))
                {
                    var backup = await ReadCatalogAsync(backupPath, cancellationToken);
                    await SaveAsync(backup, cancellationToken);
                    return new WindowPlacementLoadResult(
                        backup,
                        true,
                        $"Die Platzierungen waren ungültig und wurden als {invalidFileName} gesichert.");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception backupException) when (backupException is JsonException or InvalidDataException)
            {
                // Die ungültige Sicherung wird nicht als neuer Primärspeicher übernommen.
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
            catalog.Entries.OrderByDescending(entry => entry.LastUpdatedUtc).Take(MaximumEntries).ToArray());
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
    }
}
