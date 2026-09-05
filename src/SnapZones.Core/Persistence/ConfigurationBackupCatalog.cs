using System.Text.Json;
using SnapZones.Core.Models;

namespace SnapZones.Core.Persistence;

/// <summary>
/// Ein frueherer Stand der Konfiguration, wie ihn <see cref="JsonConfigurationRepository"/> beim Speichern
/// neben <c>settings.json</c> ablegt.
/// </summary>
/// <param name="Path">Die Sicherungsdatei.</param>
/// <param name="SavedAt">Wann dieser Stand zuletzt gueltig war: die Aenderungszeit der Datei.</param>
/// <param name="Summary">
/// Was sich zwischen diesem Stand und dem naechstjuengeren geaendert hat, in einem Satz; sonst
/// «Automatische Sicherung».
/// </param>
/// <param name="IsReadable">Falsch, wenn die Datei nicht als Konfiguration gelesen werden konnte.</param>
public sealed record ConfigurationBackup(string Path, DateTimeOffset SavedAt, string Summary, bool IsReadable);

/// <summary>
/// Liest die fuenf automatischen Sicherungen und beschreibt sie so, dass ein Anwender den richtigen Stand
/// wiederfindet. Die Sicherungen selbst schreibt das Repository; hier werden sie nur gelesen.
/// </summary>
public sealed class ConfigurationBackupCatalog
{
    public const int Capacity = 5;
    private readonly string directoryPath;
    private readonly JsonSerializerOptions serializerOptions = JsonConfigurationRepository.CreateSerializerOptions();

    public ConfigurationBackupCatalog(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        this.directoryPath = directoryPath;
    }

    /// <summary>
    /// Alle vorhandenen Sicherungen, die juengste zuerst. Die Beschreibung vergleicht jede Sicherung mit dem
    /// naechstjuengeren Stand; fuer die juengste ist das <paramref name="current"/>.
    /// </summary>
    public IReadOnlyList<ConfigurationBackup> List(SnapConfiguration? current)
    {
        var result = new List<ConfigurationBackup>(Capacity);
        var newer = current;
        for (var index = 1; index <= Capacity; index++)
        {
            var path = PathFor(index);
            if (!File.Exists(path))
            {
                continue;
            }

            var savedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero).ToLocalTime();
            var loaded = TryRead(path);
            var summary = loaded is null
                ? "Sicherung nicht lesbar"
                : ConfigurationDiff.Summarize(loaded, newer);
            result.Add(new ConfigurationBackup(path, savedAt, summary, loaded is not null));
            newer = loaded ?? newer;
        }

        return result;
    }

    /// <summary>Liest eine Sicherung vollstaendig, hebt sie auf das aktuelle Schema und prueft sie.</summary>
    public async Task<SnapConfiguration> LoadAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var loaded = await JsonSerializer.DeserializeAsync<SnapConfiguration>(stream, serializerOptions, cancellationToken);
        var upgraded = JsonConfigurationRepository.Upgrade(loaded);
        JsonConfigurationRepository.Validate(upgraded);
        return upgraded;
    }

    private string PathFor(int index) => Path.Combine(directoryPath, $"settings.backup-{index}.json");

    private SnapConfiguration? TryRead(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var loaded = JsonSerializer.Deserialize<SnapConfiguration>(stream, serializerOptions);
            var upgraded = JsonConfigurationRepository.Upgrade(loaded);
            JsonConfigurationRepository.Validate(upgraded);
            return upgraded;
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
