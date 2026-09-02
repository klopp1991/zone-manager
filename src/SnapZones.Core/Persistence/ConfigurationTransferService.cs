using System.Text.Json;
using SnapZones.Core.Models;

namespace SnapZones.Core.Persistence;

public sealed class ConfigurationTransferService
{
    private const int CurrentArchiveVersion = 1;
    private const string ProductName = "Zone Manager";

    /// <summary>
    /// Fruehere Produktnamen. Eine Sicherung, die noch unter einem alten Namen geschrieben wurde,
    /// laesst sich weiterhin einlesen; geschrieben wird nur noch der aktuelle Name.
    /// </summary>
    private static readonly string[] LegacyProductNames =
        ["Sascha’s Zone Manager", "Sascha's Zone Manager", "Sascha Window Zones"];
    private readonly JsonSerializerOptions serializerOptions = JsonConfigurationRepository.CreateSerializerOptions();

    public async Task ExportAsync(
        string filePath,
        SnapConfiguration configuration,
        string productVersion,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(productVersion);
        JsonConfigurationRepository.Validate(configuration);

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidDataException("Der Exportpfad besitzt kein gültiges Verzeichnis.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        var archive = new ConfigurationArchive(
            CurrentArchiveVersion,
            ProductName,
            productVersion,
            DateTimeOffset.UtcNow,
            configuration);

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
                await JsonSerializer.SerializeAsync(stream, archive, serializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (File.Exists(fullPath))
            {
                File.Replace(temporaryPath, fullPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, fullPath);
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

    public async Task<SnapConfiguration> ImportAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        try
        {
            await using var stream = new FileStream(
                Path.GetFullPath(filePath),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var archive = await JsonSerializer.DeserializeAsync<ConfigurationArchive>(
                stream,
                serializerOptions,
                cancellationToken);
            if (archive is null)
            {
                throw new InvalidDataException("Die Importdatei ist leer.");
            }

            var supportedProduct =
                string.Equals(archive.Product, ProductName, StringComparison.Ordinal) ||
                LegacyProductNames.Contains(archive.Product, StringComparer.Ordinal);
            if (archive.ArchiveVersion != CurrentArchiveVersion || !supportedProduct)
            {
                throw new InvalidDataException("Die Importdatei besitzt ein nicht unterstütztes Format.");
            }

            var configuration = JsonConfigurationRepository.Upgrade(archive.Configuration);
            JsonConfigurationRepository.Validate(configuration);
            return configuration;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Die Importdatei enthält kein gültiges JSON.", exception);
        }
    }

    private sealed record ConfigurationArchive(
        int ArchiveVersion,
        string Product,
        string ProductVersion,
        DateTimeOffset ExportedAtUtc,
        SnapConfiguration Configuration);
}
