using System.Text.Json;
using System.Text.Json.Serialization;
using SnapZones.Core.Geometry;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.AppRules;

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
        StaleTemporaryFiles.Remove(directoryPath, "settings.*.tmp");
        var settingsPath = Path.Combine(directoryPath, SettingsFileName);
        if (!File.Exists(settingsPath))
        {
            var recovered = await RecoverFromBackupAsync(cancellationToken);
            return recovered ?? new ConfigurationLoadResult(SnapConfiguration.CreateDefault(), false);
        }

        try
        {
            SnapConfiguration? loaded;
            await using (var stream = new FileStream(
                             settingsPath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             bufferSize: 4096,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                loaded = await JsonSerializer.DeserializeAsync<SnapConfiguration>(
                    stream,
                    serializerOptions,
                    cancellationToken);
            }

            var configuration = Upgrade(loaded);
            Validate(configuration);
            configuration = ApplyCompatibleVisualDefaults(configuration);
            if (loaded?.SchemaVersion != SnapConfiguration.CurrentSchemaVersion)
            {
                await SaveAsync(configuration, cancellationToken);
            }

            return new ConfigurationLoadResult(configuration, false);
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
                var loaded = await JsonSerializer.DeserializeAsync<SnapConfiguration>(
                    stream,
                    serializerOptions,
                    cancellationToken);
                var configuration = Upgrade(loaded);
                Validate(configuration);
                var compatible = ApplyCompatibleVisualDefaults(configuration);
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

    internal static SnapConfiguration Upgrade(SnapConfiguration? configuration)
    {
        if (configuration is null)
        {
            throw new InvalidDataException("Die Konfiguration ist leer.");
        }

        var upgraded = UpgradeSchema(configuration);

        // Schema 6: jede Monitorkennung traegt Hersteller und Modell aus dem Anzeigepfad, damit ein
        // umgesteckter Monitor wiedererkannt wird; Monitorsaetze verweisen nur auf vorhandene Layouts.
        var layouts = MainZone.Normalize(upgraded.Layouts ?? [])
            .Select(layout => string.IsNullOrWhiteSpace(layout.Monitor.HardwareId)
                ? layout with { Monitor = layout.Monitor with { HardwareId = MonitorHardwareId.FromDevicePath(layout.Monitor.StableId) } }
                : layout)
            .ToArray();
        return upgraded with
        {
            Layouts = layouts,
            MonitorOrder = upgraded.MonitorOrder ?? [],
            AppRules = upgraded.AppRules ?? [],
            AppExclusions = upgraded.AppExclusions ?? [],
            MonitorSets = MonitorSets.Prune(upgraded.MonitorSets, layouts)
        };
    }

    private static SnapConfiguration UpgradeSchema(SnapConfiguration configuration)
    {
        if (configuration.SchemaVersion == SnapConfiguration.CurrentSchemaVersion)
        {
            return configuration;
        }

        if (configuration.SchemaVersion == 5)
        {
            return configuration with
            {
                SchemaVersion = SnapConfiguration.CurrentSchemaVersion,
                MonitorSets = []
            };
        }

        if (configuration.SchemaVersion == 4)
        {
            // Schema 4 kannte noch keine Ausschluesse; bestehende Staende starten ohne einen einzigen.
            return configuration with
            {
                SchemaVersion = SnapConfiguration.CurrentSchemaVersion,
                MonitorOrder = configuration.MonitorOrder ?? [],
                AppRules = configuration.AppRules ?? [],
                AppExclusions = []
            };
        }

        if (configuration.SchemaVersion == 3)
        {
            return configuration with
            {
                SchemaVersion = SnapConfiguration.CurrentSchemaVersion,
                MonitorOrder = [],
                AppRules = configuration.AppRules ?? [],
                AppExclusions = []
            };
        }

        if (configuration.SchemaVersion == 2)
        {
            return configuration with
            {
                SchemaVersion = SnapConfiguration.CurrentSchemaVersion,
                AppRules = [],
                AppExclusions = []
            };
        }

        if (configuration.SchemaVersion != 1 || configuration.LegacyProfiles is not { Count: > 0 } profiles)
        {
            throw new InvalidDataException("Die Konfigurationsversion wird nicht unterstützt.");
        }

        var layouts = profiles
            .SelectMany(profile => profile.Monitors.Select(monitor => monitor with
            {
                Id = monitor.Id == Guid.Empty ? Guid.NewGuid() : monitor.Id,
                Name = profile.Name.Trim(),
                IsActive = profile.Id == configuration.Settings.ActiveProfileId
            }))
            .ToList();

        foreach (var group in layouts.GroupBy(MonitorKey, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count(layout => layout.IsActive) == 0)
            {
                var firstId = group.First().Id;
                var index = layouts.FindIndex(layout => layout.Id == firstId);
                layouts[index] = layouts[index] with { IsActive = true };
            }
        }

        return new SnapConfiguration(
            SnapConfiguration.CurrentSchemaVersion,
            configuration.Settings with { ActiveProfileId = Guid.Empty },
            layouts);
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

        if (configuration.Layouts.Any(layout =>
                layout.Id == Guid.Empty ||
                string.IsNullOrWhiteSpace(layout.Name)))
        {
            throw new InvalidDataException("Jedes Layout benötigt eine ID und einen Namen.");
        }

        if (configuration.Layouts.Select(layout => layout.Id).Distinct().Count() != configuration.Layouts.Count)
        {
            throw new InvalidDataException("Layout-IDs müssen eindeutig sein.");
        }

        if (configuration.MonitorNames is null ||
            configuration.MonitorNames.Any(entry =>
                string.IsNullOrWhiteSpace(entry.Key) ||
                string.IsNullOrWhiteSpace(entry.Value) ||
                entry.Value != entry.Value.Trim() ||
                entry.Value.Length > MonitorNaming.MaximumCustomNameLength) ||
            configuration.MonitorNames.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != configuration.MonitorNames.Count)
        {
            throw new InvalidDataException("Die gespeicherten Monitornamen sind ungültig.");
        }

        if (configuration.MonitorOrder is null ||
            configuration.MonitorOrder.Any(string.IsNullOrWhiteSpace) ||
            configuration.MonitorOrder.Any(key => key != key.Trim()) ||
            configuration.MonitorOrder.Distinct(StringComparer.OrdinalIgnoreCase).Count() != configuration.MonitorOrder.Count)
        {
            throw new InvalidDataException("Die gespeicherte Monitorreihenfolge ist ungültig.");
        }

        var layoutIds = configuration.Layouts.Select(layout => layout.Id).ToHashSet();
        if (configuration.MonitorSets is null ||
            configuration.MonitorSets.Any(set =>
                string.IsNullOrWhiteSpace(set.SetKey) ||
                set.ActiveLayouts is null ||
                set.ActiveLayouts.Any(entry => string.IsNullOrWhiteSpace(entry.Key) || !layoutIds.Contains(entry.Value))) ||
            configuration.MonitorSets.Select(set => set.SetKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != configuration.MonitorSets.Count)
        {
            throw new InvalidDataException("Die gespeicherten Monitorsätze sind ungültig.");
        }

        foreach (var group in configuration.Layouts.GroupBy(MonitorKey, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count(layout => layout.IsActive) != 1)
            {
                throw new InvalidDataException("Pro Monitor muss genau ein Layout aktiv sein.");
            }

            var duplicateName = group
                .GroupBy(layout => layout.Name.Trim(), StringComparer.CurrentCultureIgnoreCase)
                .Any(names => names.Count() > 1);
            if (duplicateName)
            {
                throw new InvalidDataException("Layoutnamen müssen pro Monitor eindeutig sein.");
            }
        }

        ValidateSettings(configuration.Settings);
        ValidateAppRules(configuration.AppRules);
        ValidateAppExclusions(configuration.AppExclusions);

        foreach (var layout in configuration.Layouts)
        {
            if (layout.SavedWidth <= 0 || layout.SavedHeight <= 0)
            {
                throw new InvalidDataException($"Das Layout «{layout.Name}» enthält eine ungültige Monitorgrösse.");
            }

            if (!ZoneGeometry.Validate(layout.Zones).IsValid)
            {
                throw new InvalidDataException($"Das Layout «{layout.Name}» enthält ungültige Zonen.");
            }

            if (layout.MainZoneId is Guid mainZoneId && layout.Zones.All(zone => zone.Id != mainZoneId))
            {
                throw new InvalidDataException($"Die Hauptzone des Layouts «{layout.Name}» gibt es nicht.");
            }
        }
    }

    private static string MonitorKey(MonitorLayout layout) =>
        !string.IsNullOrWhiteSpace(layout.Monitor.StableId)
            ? $"stable:{layout.Monitor.StableId}"
            : $"device:{layout.Monitor.DeviceName}";

    private static void ValidateSettings(AppSettings settings)
    {
        if (!Enum.IsDefined(settings.OverlayScope) ||
            !Enum.IsDefined(settings.TriggerMode) ||
            !Enum.IsDefined(settings.ThemeMode) ||
            !Enum.IsDefined(settings.ElevationMode))
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

    private static void ValidateAppRules(IReadOnlyList<AppRule>? rules)
    {
        if (rules is null ||
            rules.Any(rule =>
                rule.Id == Guid.Empty ||
                string.IsNullOrWhiteSpace(rule.ProcessPath) ||
                rule.ProcessPath != rule.ProcessPath.Trim() ||
                rule.ProcessPath.Length > 1024 ||
                rule.TargetLayoutId == Guid.Empty ||
                rule.TargetZoneId == Guid.Empty ||
                !Enum.IsDefined(rule.Event) ||
                rule.DelayMilliseconds is < 0 or > 30000 ||
                rule.RetryCount is < 0 or > 3 ||
                rule.Priority is < 0 or > 100 ||
                InvalidOptionalPattern(rule.WindowTitlePattern, 512) ||
                InvalidOptionalPattern(rule.WindowClass, 256)) ||
            rules.Select(rule => rule.Id).Distinct().Count() != rules.Count)
        {
            throw new InvalidDataException("Die gespeicherten App-Regeln sind ungültig.");
        }
    }

    private static void ValidateAppExclusions(IReadOnlyList<AppExclusion>? exclusions)
    {
        if (exclusions is null ||
            exclusions.Any(exclusion =>
                exclusion.Id == Guid.Empty ||
                exclusion.ProcessPath is null ||
                exclusion.ProcessPath != exclusion.ProcessPath.Trim() ||
                exclusion.ProcessPath.Length > 1024 ||
                !exclusion.HasCriteria ||
                InvalidOptionalPattern(exclusion.WindowTitlePattern, 512) ||
                InvalidOptionalPattern(exclusion.WindowClass, 256)) ||
            exclusions.Select(exclusion => exclusion.Id).Distinct().Count() != exclusions.Count)
        {
            throw new InvalidDataException("Die gespeicherten Ausschlüsse sind ungültig.");
        }
    }

    private static bool InvalidOptionalPattern(string? value, int maximumLength) =>
        value is not null &&
        (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Length > maximumLength);
}
