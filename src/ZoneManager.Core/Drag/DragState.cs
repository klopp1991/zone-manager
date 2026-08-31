using ZoneManager.Core.Models;
using ZoneManager.Core.Monitors;

namespace ZoneManager.Core.Drag;

public enum DragState
{
    Idle,
    Tracking
}

public sealed record DragMonitorTarget(LiveMonitor Monitor, IReadOnlyList<ZoneDefinition> Zones);
