using SnapZones.Core.Models;
using SnapZones.Core.Monitors;

namespace SnapZones.Core.PartMonitors;

public sealed record PartMonitorTarget(
    LiveMonitor Monitor,
    IReadOnlyList<ZoneDefinition> PartMonitors);
