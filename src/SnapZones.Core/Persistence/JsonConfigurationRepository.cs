using System.Text.Json;
using System.Text.Json.Serialization;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.Core.Persistence;

public sealed class JsonConfigurationRepository : IConfigurationRepository
{
    private const string SettingsFileName = "settings.json";
    private readonly string directoryPath;
    private readonly JsonSerializerOptions serializerOptions;

    public JsonConfigurationRepository(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        this.directoryPath = directoryPath;
        serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
        serializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public async Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        var settingsPath = Path.Combine(directoryPath, SettingsFileName);
        if (!File.Exists(settingsPath))
        {
            return new ConfigurationLoadResult(SnapConfiguration.CreateDefault(), false);
        }

        try
        {
            await using var stream = new FileStream(
                settingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var configuration = await JsonSerializer.DeserializeAsync<SnapConfiguration>(
                stream,
                serializerOptions,
                cancellationToken);
            Validate(configuration);
            return new ConfigurationLoadResult(configuration!, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidDataException)
        {
            Directory.CreateDirectory(directoryPath);
            var backupName = $"settings.invalid-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json";
            File.Move(settingsPath, Path.Combine(directoryPath, backupName));
            return new ConfigurationLoadResult(
                SnapConfiguration.CreateDefault(),
                true,
                $"Die Konfiguration war ungültig und wurde als {backupName} gesichert.");
        }
    }

    public async Task SaveAsync(SnapConfiguration configuration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Validate(configuration);
        Directory.CreateDirectory(directoryPath);

        var settingsPath = Path.Combine(directoryPath, SettingsFileName);
        var temporaryPath = Path.Combine(directoryPath, $"settings.{Guid.NewGuid():N}.tmp");
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
                await JsonSerializer.SerializeAsync(stream, configuration, serializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, settingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void Validate(SnapConfiguration? configuration)
    {
        if (configuration is null)
        {
            throw new InvalidDataException("Die Konfiguration ist leer.");
        }

        if (configuration.SchemaVersion != SnapConfiguration.CurrentSchemaVersion)
        {
            throw new InvalidDataException("Die Konfigurationsversion wird nicht unterstützt.");
        }

        if (configuration.Profiles.Count == 0 || configuration.Profiles.Any(profile => string.IsNullOrWhiteSpace(profile.Name)))
        {
            throw new InvalidDataException("Mindestens ein benanntes Profil ist erforderlich.");
        }

        if (configuration.Profiles.Select(profile => profile.Id).Distinct().Count() != configuration.Profiles.Count)
        {
            throw new InvalidDataException("Profil-IDs müssen eindeutig sein.");
        }

        foreach (var profile in configuration.Profiles)
        {
            foreach (var monitor in profile.Monitors)
            {
                if (!ZoneGeometry.Validate(monitor.Zones).IsValid)
                {
                    throw new InvalidDataException($"Das Profil «{profile.Name}» enthält ungültige Zonen.");
                }
            }
        }
    }
}
