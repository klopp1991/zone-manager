using ZoneManager.Core.Models;

namespace ZoneManager.Core.Persistence;

public sealed record ConfigurationLoadResult(
    SnapConfiguration Configuration,
    bool RecoveredFromError,
    string? ErrorMessage = null);
