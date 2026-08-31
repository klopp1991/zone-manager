using ZoneManager.Core.Models;

namespace ZoneManager.Core.Monitors;

public enum MonitorMatchQuality
{
    StableId,
    DeviceName,
    Resolution,
    PrimaryFallback,
    Missing
}

public sealed record MonitorMatch(
    MonitorLayout Saved,
    LiveMonitor? Live,
    MonitorMatchQuality Quality);
