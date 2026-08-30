using SnapZones.Core.Models;

namespace SnapZones.Core.Persistence;

public sealed record ConfigurationLoadResult(
    SnapConfiguration Configuration,
    bool RecoveredFromError,
    string? ErrorMessage = null);
