namespace ZoneManager.Core.Persistence;

public enum ConfigurationMigrationStatus
{
    /// <summary>Es gibt keinen alten Ordner; es ist nichts zu übernehmen.</summary>
    NothingToMigrate,

    /// <summary>Der Inhalt des alten Ordners wurde übernommen.</summary>
    Migrated,

    /// <summary>Am neuen Ort liegt bereits eine Konfiguration; sie hat Vorrang und bleibt unverändert.</summary>
    TargetAlreadyPresent,

    /// <summary>Die Übernahme ist fehlgeschlagen; der alte Ordner bleibt unverändert.</summary>
    Failed
}

public sealed record ConfigurationMigrationResult(
    ConfigurationMigrationStatus Status,
    int CopiedFileCount,
    string Message);

/// <summary>
/// Übernimmt beim ersten Start den Inhalt des alten Konfigurationsordners in den neuen.
/// Die Übernahme ist idempotent, überschreibt nichts und löscht den alten Ordner nicht:
/// er bleibt als Rückfallebene erhalten.
/// </summary>
public static class ConfigurationDirectoryMigration
{
    public static ConfigurationMigrationResult Run(string legacyDirectory, string targetDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        try
        {
            if (!Directory.Exists(legacyDirectory))
            {
                return new ConfigurationMigrationResult(
                    ConfigurationMigrationStatus.NothingToMigrate,
                    0,
                    $"Kein alter Konfigurationsordner unter {legacyDirectory}.");
            }

            if (ContainsFiles(targetDirectory))
            {
                return new ConfigurationMigrationResult(
                    ConfigurationMigrationStatus.TargetAlreadyPresent,
                    0,
                    $"Die Konfiguration unter {targetDirectory} hat Vorrang; es wurde nichts übernommen.");
            }

            var copied = CopyDirectory(legacyDirectory, targetDirectory);
            return new ConfigurationMigrationResult(
                ConfigurationMigrationStatus.Migrated,
                copied,
                $"{copied} Datei(en) von {legacyDirectory} nach {targetDirectory} übernommen; der alte Ordner bleibt erhalten.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ConfigurationMigrationResult(
                ConfigurationMigrationStatus.Failed,
                0,
                $"Die Konfiguration konnte nicht übernommen werden: {exception.Message}");
        }
    }

    private static bool ContainsFiles(string directory) =>
        Directory.Exists(directory) &&
        Directory.EnumerateFileSystemEntries(directory).Any();

    private static int CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        var copied = 0;
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
            copied++;
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            copied += CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }

        return copied;
    }
}
