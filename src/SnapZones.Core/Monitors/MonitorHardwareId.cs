using System.Text.RegularExpressions;

namespace SnapZones.Core.Monitors;

/// <summary>
/// Leitet eine anschlussunabhaengige Kennung eines Monitors ab. Der Anzeigepfad von Windows hat die
/// Form <c>\\?\DISPLAY#GSM9EB9#5&amp;4ace297&amp;1&amp;UID4357#{...}</c>: das zweite Segment ist Hersteller
/// und Modell aus der EDID, das dritte der Anschluss. Nur das zweite bleibt beim Umstecken gleich.
/// </summary>
public static partial class MonitorHardwareId
{
    [GeneratedRegex(@"DISPLAY#(?<model>[^#\\]+)#", RegexOptions.IgnoreCase)]
    private static partial Regex ModelSegment();

    /// <summary>Hersteller und Modell aus einem Anzeigepfad, oder leer.</summary>
    public static string FromDevicePath(string? devicePath)
    {
        if (string.IsNullOrWhiteSpace(devicePath))
        {
            return string.Empty;
        }

        var match = ModelSegment().Match(devicePath);
        return match.Success ? match.Groups["model"].Value.Trim().ToUpperInvariant() : string.Empty;
    }

    /// <summary>Kennung aus Modell und Seriennummer; ohne Seriennummer nur das Modell.</summary>
    public static string Compose(string? model, string? serialNumber)
    {
        var normalizedModel = (model ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizedModel.Length == 0)
        {
            return string.Empty;
        }

        var normalizedSerial = (serialNumber ?? string.Empty).Trim();
        return normalizedSerial.Length == 0 ? normalizedModel : $"{normalizedModel}#{normalizedSerial}";
    }

    /// <summary>Das Modell ohne Seriennummer, fuer Vergleiche mit Kennungen ohne Seriennummer.</summary>
    public static string ModelOf(string hardwareId)
    {
        var separator = hardwareId.IndexOf('#');
        return separator < 0 ? hardwareId : hardwareId[..separator];
    }
}
