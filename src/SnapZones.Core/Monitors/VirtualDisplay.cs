using SnapZones.Core.Models;

namespace SnapZones.Core.Monitors;

/// <summary>
/// Kennzeichen des virtuellen Monitors, den Zone Manager selbst ueber den Vollbildzonen-Treiber
/// «Virtual Display Driver» anlegt, um ein Programm im Vollbild auf Zonengroesse zu halten. Der
/// Treiber meldet den Adapter mit der Hardware-Kennung <c>Root\MttVDD</c> und den Monitor mit der
/// EDID-Kennung <c>MTT1337</c> im Anzeigepfad (<c>\\?\DISPLAY#MTT1337#…</c>). Fuer die eigene
/// Monitorsicht ist er kein Monitor: kein Layout, kein Eintrag in der Liste, kein Ziel fuer Regeln.
/// </summary>
public static class VirtualDisplay
{
    /// <summary>Hersteller und Modell aus der EDID des Treibers, wie <see cref="MonitorHardwareId"/> sie liefert.</summary>
    public const string MonitorModel = "MTT1337";

    /// <summary>PnP-Hardware-Kennung des Adapters, unter der der Treiber installiert wird.</summary>
    public const string AdapterHardwareId = @"Root\MttVDD";

    /// <summary>Geraetebeschreibung des Adapters aus der INF-Datei des Treibers.</summary>
    public const string AdapterDescription = "Virtual Display Driver";

    /// <summary>Ob diese Kennung den virtuellen Monitor des Treibers bezeichnet.</summary>
    public static bool IsVirtual(MonitorIdentity monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        return string.Equals(MonitorHardwareId.ModelOf(monitor.HardwareId ?? string.Empty), MonitorModel, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(MonitorHardwareId.FromDevicePath(monitor.StableId), MonitorModel, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ob ein Schluessel aus Namen, Reihenfolge oder Monitorkombination den virtuellen Monitor nennt.</summary>
    public static bool MentionsVirtual(string key) =>
        key.Contains(MonitorModel, StringComparison.OrdinalIgnoreCase);
}
