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

public sealed record AppSettings(
    Guid ActiveProfileId,
    bool SnappingEnabled,
    bool StartWithWindows,
    OverlayScope OverlayScope,
    TriggerMode TriggerMode,
    int OuterMargin,
    int ZoneGap,
    string OverlayColor,
    double OverlayOpacity)
{
    public static AppSettings Default(Guid activeProfileId) => new(
        activeProfileId,
        SnappingEnabled: false,
        StartWithWindows: false,
        OverlayScope.AllMonitors,
        TriggerMode.Immediate,
        OuterMargin: 8,
        ZoneGap: 8,
        OverlayColor: "#2F6FED",
        OverlayOpacity: 0.24);
}
