namespace SnapZones.Core.Models;

public sealed record LayoutProfile(
    Guid Id,
    string Name,
    int? QuickSlot,
    IReadOnlyList<MonitorLayout> Monitors);
