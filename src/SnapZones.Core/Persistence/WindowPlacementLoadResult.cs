using SnapZones.Core.Placement;

namespace SnapZones.Core.Persistence;

public sealed record WindowPlacementLoadResult(
    WindowPlacementCatalog Catalog,
    bool RecoveredFromError,
    string? ErrorMessage = null);
