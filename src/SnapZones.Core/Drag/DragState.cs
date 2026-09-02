using SnapZones.Core.Models;
using SnapZones.Core.Monitors;

namespace SnapZones.Core.Drag;

public enum DragState
{
    Idle,
    Tracking
}

public sealed record DragMonitorTarget(LiveMonitor Monitor, IReadOnlyList<ZoneDefinition> Zones);
