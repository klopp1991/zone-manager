using SnapZones.Core.Models;
using SnapZones.Core.Placement;

namespace SnapZones.App.Services;

public sealed record PlacementEnvironment(
    SnapConfiguration Configuration,
    IReadOnlyList<PlacementMonitorTarget> Monitors,
    IReadOnlyList<PlacementZoneTarget> Zones);
