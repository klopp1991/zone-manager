using System.Text.Json.Serialization;

namespace SnapZones.Core.Models;

public enum OverlayScope
{
    /// <summary>Die Zonen erscheinen gleichzeitig auf jedem Monitor.</summary>
    AllMonitors,

    /// <summary>
    /// Die Zonen erscheinen nur auf dem Monitor, auf dem das Ziehen begonnen hat, und bleiben dort.
    /// Wandert der Zeiger auf einen anderen Monitor, sieht er dort keine Zonen.
    /// </summary>
    ActiveMonitor,

    /// <summary>
    /// Die Zonen wandern mit: sie erscheinen immer auf dem Monitor unter dem Mauszeiger und
    /// verschwinden auf allen uebrigen. Der neue Wert steht am Ende, damit bestehende gespeicherte
    /// Konfigurationen ihre Bedeutung behalten.
    /// </summary>
    CursorMonitor
}

public enum TriggerMode
{
    Immediate,
    ShiftKey
}

public enum ThemeMode
{
    System,
    Light,
    Dark
}

public sealed record AppSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    Guid ActiveProfileId,
    bool SnappingEnabled,
    bool StartWithWindows,
    OverlayScope OverlayScope,
    TriggerMode TriggerMode,
    int OuterMargin,
    int ZoneGap,
    string OverlayColor,
    double OverlayOpacity,
    ThemeMode ThemeMode = ThemeMode.System,
    int MagnetThresholdPixels = 20,
    bool ShowZoneNames = true,
    EdgeInsets? OuterMargins = null,
    bool RememberWindowPositions = true,
    bool CheckForUpdatesOnStart = false,
    ElevationMode ElevationMode = ElevationMode.WhenNeeded)
{
    public EdgeInsets EffectiveOuterMargins =>
        (OuterMargins ?? EdgeInsets.Uniform(OuterMargin)).Clamp(0, 400);

    public static AppSettings Default(Guid activeProfileId) => new(
        activeProfileId,
        SnappingEnabled: false,
        StartWithWindows: false,
        OverlayScope.AllMonitors,
        TriggerMode.Immediate,
        OuterMargin: 8,
        ZoneGap: 0,
        OverlayColor: "#707070",
        OverlayOpacity: 0.24,
        ThemeMode: ThemeMode.System,
        MagnetThresholdPixels: 20,
        ShowZoneNames: true,
        OuterMargins: EdgeInsets.Uniform(8),
        RememberWindowPositions: true,
        CheckForUpdatesOnStart: false,
        ElevationMode: ElevationMode.WhenNeeded);
}
