using ZoneManager.Core.Placement;

namespace ZoneManager.Core.Persistence;

public sealed record WindowPlacementLoadResult(
    WindowPlacementCatalog Catalog,
    bool RecoveredFromError,
    string? ErrorMessage = null);
