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

/// <summary>Was mit einem Fenster geschieht, das seine Groesse nicht aendern kann und die Zone nicht fuellt.</summary>
public enum FixedSizeWindowPlacement
{
    /// <summary>Mittig in der Zone ablegen.</summary>
    Center,

    /// <summary>An der linken oberen Ecke der Zone ausrichten.</summary>
    TopLeft,

    /// <summary>Nicht anfassen; das Fenster bleibt, wo es ist.</summary>
    Leave
}

/// <summary>Die Zusatztasten der Zonenkuerzel. Die eigentliche Taste (Pfeil, Ziffer, Ruecktaste) bleibt gleich.</summary>
public enum ZoneHotkeyModifiers
{
    /// <summary>
    /// Nicht mehr die Voreinstellung: Windows erzeugt aus AltGr intern Strg + Alt, sodass ein globales
    /// Kuerzel mit diesen Zusatztasten alle AltGr-Zeichen auf denselben Tasten schluckt — auf einer
    /// Schweizer Tastatur unter anderem @ (AltGr + 2), # (AltGr + 3) und | (AltGr + 7).
    /// </summary>
    ControlAlt,
    ControlShift,
    AltShift,
    ControlWin
}

/// <summary>Was die Beschriftung einer Zone im Overlay zeigt.</summary>
public enum OverlayLabelStyle
{
    NumberAndName,
    NumberOnly,
    NameOnly
}

/// <summary>
/// Alle Einstellungen. Die Grundwerte reichen fuer den Alltag; die ab dem 02.09.2026 hinzugekommenen
/// Feinabstimmungen (Toleranzen, Verzoegerungen, Overlay-Stil, Schutzgrenzen, Zusatztasten) richten sich
/// an erfahrene Anwender und sind in der Oberflaeche erst nach «Erweiterte Einstellungen anzeigen»
/// sichtbar. Jeder Wert hat einen sicheren Standard; <see cref="Default"/> ist zugleich das Zurücksetzen.
/// </summary>
public sealed record AppSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    Guid ActiveProfileId,
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
    ElevationMode ElevationMode = ElevationMode.WhenNeeded,
    bool ShowAdvancedSettings = false,
    int OverlayShowDelayMilliseconds = 0,
    bool ActivateWindowAfterSnap = false,
    bool RestoreSizeWhenLeavingZone = false,
    FixedSizeWindowPlacement FixedSizeWindowPlacement = FixedSizeWindowPlacement.Center,
    int PlacementTolerancePixels = 2,
    int SnappedTolerancePixels = 40,
    bool CatchNewWindowsInMainZone = true,
    bool PreferRememberedZone = true,
    bool RestoreMaximizedWindows = true,
    int RememberedWindowLimit = 500,
    int NewWindowSettleDelayMilliseconds = 0,
    int RuleRetryDelayMilliseconds = 250,
    bool ZoneHotkeysEnabled = true,
    ZoneHotkeyModifiers ZoneHotkeyModifiers = ZoneHotkeyModifiers.ControlShift,
    OverlayLabelStyle OverlayLabelStyle = OverlayLabelStyle.NumberAndName,
    int OverlayBorderThickness = 1,
    int OverlayCornerRadius = 4,
    int OverlayLabelFontSize = 13,
    string HighlightColor = "",
    double HighlightOpacity = 0.36,
    int MoveHookEventLimit = 400,
    int DragWatchdogSeconds = 120)
{
    [JsonIgnore]
    public EdgeInsets EffectiveOuterMargins =>
        (OuterMargins ?? EdgeInsets.Uniform(OuterMargin)).Clamp(0, 400);

    /// <summary>Die Farbe der hervorgehobenen Zone; ohne eigene Angabe die Zonenfarbe.</summary>
    [JsonIgnore]
    public string EffectiveHighlightColor =>
        string.IsNullOrWhiteSpace(HighlightColor) ? OverlayColor : HighlightColor;

    public static AppSettings Default(Guid activeProfileId) => new(
        activeProfileId,
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
