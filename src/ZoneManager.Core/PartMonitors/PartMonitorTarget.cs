using ZoneManager.Core.Models;
using ZoneManager.Core.Monitors;

namespace ZoneManager.Core.PartMonitors;

public sealed record PartMonitorTarget(
    LiveMonitor Monitor,
    IReadOnlyList<ZoneDefinition> PartMonitors);
