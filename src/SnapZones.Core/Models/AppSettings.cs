using SnapZones.Core.Placement;

namespace SnapZones.Core.Models;

public enum OverlayScope
{
    AllMonitors,
    ActiveMonitor
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
    int MagnetThresholdPixels = 10,
    bool ShowZoneNames = true,
    EdgeInsets? OuterMargins = null,
    bool RestoreWindowPlacementEnabled = true,
    IReadOnlyList<WindowPlacementRule>? WindowPlacementRules = null)
{
    public EdgeInsets EffectiveOuterMargins =>
        (OuterMargins ?? EdgeInsets.Uniform(OuterMargin)).Clamp(0, 400);

    public IReadOnlyList<WindowPlacementRule> EffectiveWindowPlacementRules => WindowPlacementRules ?? [];

    public static AppSettings Default(Guid activeProfileId) => new(
        activeProfileId,
        SnappingEnabled: false,
        StartWithWindows: false,
        OverlayScope.AllMonitors,
        TriggerMode.Immediate,
        OuterMargin: 8,
        ZoneGap: 8,
        OverlayColor: "#707070",
        OverlayOpacity: 0.24,
        ThemeMode: ThemeMode.System,
        MagnetThresholdPixels: 10,
        ShowZoneNames: true,
        OuterMargins: EdgeInsets.Uniform(8),
        RestoreWindowPlacementEnabled: true);
}
