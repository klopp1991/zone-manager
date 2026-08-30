using System.Text.Json;
using System.Text.Json.Serialization;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.Core.Persistence;

public sealed class JsonConfigurationRepository : IConfigurationRepository
{
    private const string SettingsFileName = "settings.json";
    private const int BackupCount = 5;
    private readonly string directoryPath;
    private readonly JsonSerializerOptions serializerOptions;

    public JsonConfigurationRepository(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        this.directoryPath = directoryPath;
        serializerOptions = CreateSerializerOptions();
    }

    public async Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        var settingsPath = Path.Combine(directoryPath, SettingsFileName);
        if (!File.Exists(settingsPath))
        {
            var recovered = await RecoverFromBackupAsync(cancellationToken);
            return recovered ?? new ConfigurationLoadResult(SnapConfiguration.CreateDefault(), false);
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
            return new ConfigurationLoadResult(ApplyCompatibleVisualDefaults(configuration!), false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            Directory.CreateDirectory(directoryPath);
            var backupName = $"settings.invalid-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json";
            File.Move(settingsPath, Path.Combine(directoryPath, backupName));
            var recovered = await RecoverFromBackupAsync(cancellationToken);
            return recovered is null
                ? new ConfigurationLoadResult(
                    SnapConfiguration.CreateDefault(),
                    true,
                    $"Die Konfiguration war ungültig und wurde als {backupName} gesichert.")
                : recovered with
                {
                    ErrorMessage = $"Die Konfiguration war ungültig, wurde als {backupName} gesichert und aus einer Sicherung wiederhergestellt."
                };
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

            if (File.Exists(settingsPath))
            {
                RotateBackups();
                File.Replace(temporaryPath, settingsPath, BackupPath(1), ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, settingsPath);
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

    private async Task<ConfigurationLoadResult?> RecoverFromBackupAsync(CancellationToken cancellationToken)
    {
        for (var index = 1; index <= BackupCount; index++)
        {
            var backupPath = BackupPath(index);
            if (!File.Exists(backupPath))
            {
                continue;
            }

            try
            {
                await using var stream = new FileStream(
                    backupPath,
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
                var compatible = ApplyCompatibleVisualDefaults(configuration!);
                await SaveAsync(compatible, cancellationToken);
                return new ConfigurationLoadResult(
                    compatible,
                    true,
                    $"Die Konfiguration wurde aus Sicherung {index} wiederhergestellt.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is JsonException or IOException or InvalidDataException)
            {
                // Eine einzelne beschädigte Sicherung darf ältere Sicherungen nicht blockieren.
            }
        }

        return null;
    }

    private void RotateBackups()
    {
        for (var index = BackupCount; index >= 2; index--)
        {
            var sourcePath = BackupPath(index - 1);
            if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, BackupPath(index), overwrite: true);
            }
        }
    }

    private string BackupPath(int index) => Path.Combine(directoryPath, $"settings.backup-{index}.json");

    private static SnapConfiguration ApplyCompatibleVisualDefaults(SnapConfiguration configuration)
    {
        if (!string.Equals(configuration.Settings.OverlayColor, "#2F6FED", StringComparison.OrdinalIgnoreCase))
        {
            return configuration;
        }

        return configuration with
        {
            Settings = configuration.Settings with { OverlayColor = "#707070" }
        };
    }

    internal static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    internal static void Validate(SnapConfiguration? configuration)
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

        if (configuration.Profiles.All(profile => profile.Id != configuration.Settings.ActiveProfileId))
        {
            throw new InvalidDataException("Das aktive Profil fehlt in der Konfiguration.");
        }

        ValidateSettings(configuration.Settings);

        foreach (var profile in configuration.Profiles)
        {
            foreach (var monitor in profile.Monitors)
            {
                if (monitor.SavedWidth <= 0 || monitor.SavedHeight <= 0)
                {
                    throw new InvalidDataException($"Das Profil «{profile.Name}» enthält eine ungültige Monitorgrösse.");
                }

                if (!ZoneGeometry.Validate(monitor.Zones).IsValid)
                {
                    throw new InvalidDataException($"Das Profil «{profile.Name}» enthält ungültige Zonen.");
                }
            }
        }
    }

    private static void ValidateSettings(AppSettings settings)
    {
        if (!Enum.IsDefined(settings.OverlayScope) ||
            !Enum.IsDefined(settings.TriggerMode) ||
            !Enum.IsDefined(settings.ThemeMode))
        {
            throw new InvalidDataException("Die Konfiguration enthält einen unbekannten Einstellungswert.");
        }

        if (settings.OuterMargin is < 0 or > 400 ||
            settings.ZoneGap is < 0 or > 80 ||
            settings.MagnetThresholdPixels is < 0 or > 40 ||
            !double.IsFinite(settings.OverlayOpacity) ||
            settings.OverlayOpacity is < 0.08 or > 0.75)
        {
            throw new InvalidDataException("Eine numerische Einstellung liegt ausserhalb des gültigen Bereichs.");
        }

        if (settings.OuterMargins is { } margins &&
            (margins.Left is < 0 or > 400 ||
             margins.Top is < 0 or > 400 ||
             margins.Right is < 0 or > 400 ||
             margins.Bottom is < 0 or > 400))
        {
            throw new InvalidDataException("Ein äusserer Abstand liegt ausserhalb des gültigen Bereichs.");
        }

        if (string.IsNullOrEmpty(settings.OverlayColor) ||
            settings.OverlayColor.Length != 7 ||
            settings.OverlayColor[0] != '#' ||
            !settings.OverlayColor.AsSpan(1).ToString().All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("Die Overlayfarbe muss das Format #RRGGBB besitzen.");
        }
    }
}
