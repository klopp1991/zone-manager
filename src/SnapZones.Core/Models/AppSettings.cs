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
    NameOnly,

    /// <summary>Gar keine Beschriftung, auch wenn die Zonennamen sonst eingeschaltet sind.</summary>
    None
}

/// <summary>
/// Die Tastenkombination des Not-Aus. Sie haelt das Einrasten an, ohne das Programm zu beenden, und
/// bleibt registriert, auch wenn die Zonenkuerzel ausgeschaltet sind.
/// </summary>
public enum EmergencyHotkey
{
    ControlAltShiftF12,
    ControlAltShiftF11,
    ControlAltShiftPause,
    Off
}

/// <summary>
/// Alle Einstellungen. Die Grundwerte reichen fuer den Alltag; die ab dem 02.09.2026 hinzugekommenen
/// Feinabstimmungen (Toleranzen, Verzoegerungen, Overlay-Stil, Schutzgrenzen, Zusatztasten) stehen seit
/// dem 05.09.2026 offen auf der Seite «Verhalten», jede mit einer «?»-Erklaerung. Der fruehere Schalter
/// «Erweiterte Einstellungen anzeigen» ist entfallen; ein gespeicherter Wert wird beim Laden ignoriert.
/// Jeder Wert hat einen sicheren Standard; <see cref="Default"/> ist zugleich das Zurücksetzen.
/// </summary>
/// <param name="EditorValuePanelOpen">
/// Ob das Werte-Panel im Layout-Editor ausgeklappt ist. Eine reine Oberflaechenvorliebe, die wie jede
/// andere Einstellung gespeichert wird, damit der Editor so aufgeht, wie er verlassen wurde.
/// </param>
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
    bool EditorValuePanelOpen = true,
    int OverlayShowDelayMilliseconds = 0,
    bool ActivateWindowAfterSnap = false,
    bool RestoreSizeWhenLeavingZone = false,
    FixedSizeWindowPlacement FixedSizeWindowPlacement = FixedSizeWindowPlacement.Center,
    int PlacementTolerancePixels = 2,
    int SnappedTolerancePixels = 40,
    [property: JsonPropertyName("CatchNewWindowsInMainZone")]
    bool CatchNewWindowsInStartZone = true,
    bool PreferRememberedZone = true,
    bool RestoreMaximizedWindows = true,
    int RememberedWindowLimit = 500,
    int NewWindowSettleDelayMilliseconds = 0,
    int RuleRetryDelayMilliseconds = 250,
    bool ZoneHotkeysEnabled = true,
    ZoneHotkeyModifiers ZoneHotkeyModifiers = ZoneHotkeyModifiers.ControlShift,
    OverlayLabelStyle OverlayLabelStyle = OverlayLabelStyle.NumberAndName,
    int OverlayBorderThickness = 2,
    int OverlayCornerRadius = 6,
    int OverlayLabelFontSize = 16,
    string HighlightColor = "#2F6FED",
    double HighlightOpacity = 0.36,
    int MoveHookEventLimit = 2000,
    int DragWatchdogSeconds = 10)
{
    /// <summary>
    /// Ob eine Zone, die als Vollbildzone markiert ist, tatsaechlich als eigener Bildschirm gilt.
    /// Ohne den Vollbildzonen-Treiber bleibt der Schalter wirkungslos.
    /// </summary>
    public bool UseFullscreenZones { get; init; } = true;

    /// <summary>Ob das ✕ des Hauptfensters das Programm nur in den Infobereich legt statt es zu beenden.</summary>
    public bool CloseToTray { get; init; } = true;

    /// <summary>Die Tastenkombination des Not-Aus.</summary>
    public EmergencyHotkey EmergencyHotkey { get; init; } = EmergencyHotkey.ControlAltShiftF12;

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
