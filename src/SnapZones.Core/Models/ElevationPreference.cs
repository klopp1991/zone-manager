using System.Text.Json;

namespace SnapZones.Core.Models;

/// <summary>
/// Liest allein die Rechte-Einstellung aus der gespeicherten Konfiguration.
///
/// Die Entscheidung, ob sich das Programm erhöht, fällt bevor irgendetwas geladen ist — vor dem
/// Hauptfenster, vor dem Protokoll, vor der eigentlichen Konfiguration. Ein Fehlschlag beim Lesen ist
/// deshalb kein Fehler, sondern führt zur zurückhaltenden Voreinstellung: gewöhnliche Rechte.
/// </summary>
public static class ElevationPreference
{
    public const string FileName = "settings.json";

    public static ElevationMode Read(string configurationDirectory)
    {
        if (string.IsNullOrWhiteSpace(configurationDirectory))
        {
            return ElevationMode.WhenNeeded;
        }

        try
        {
            var path = Path.Combine(configurationDirectory, FileName);
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : ElevationMode.WhenNeeded;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ElevationMode.WhenNeeded;
        }
    }

    /// <summary>
    /// Zieht <c>Settings.ElevationMode</c> aus dem gespeicherten JSON. Eine beschädigte oder ältere
    /// Datei ohne dieses Feld ergibt die Voreinstellung.
    /// </summary>
    public static ElevationMode Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ElevationMode.WhenNeeded;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("Settings", out var settings) ||
                settings.ValueKind != JsonValueKind.Object ||
                !settings.TryGetProperty("ElevationMode", out var mode) ||
                mode.ValueKind != JsonValueKind.String)
            {
                return ElevationMode.WhenNeeded;
            }

            return Enum.TryParse<ElevationMode>(mode.GetString(), ignoreCase: true, out var parsed)
                ? parsed
                : ElevationMode.WhenNeeded;
        }
        catch (JsonException)
        {
            return ElevationMode.WhenNeeded;
        }
    }
}
