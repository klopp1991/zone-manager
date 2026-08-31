using ZoneManager.Core.Models;
using ZoneManager.Core.Placement;

namespace ZoneManager.App.Services;

public sealed record PlacementEnvironment(
    SnapConfiguration Configuration,
    IReadOnlyList<PlacementMonitorTarget> Monitors,
    IReadOnlyList<PlacementZoneTarget> Zones);
