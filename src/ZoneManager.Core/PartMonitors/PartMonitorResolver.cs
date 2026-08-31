using ZoneManager.Core.Geometry;
using ZoneManager.Core.Models;

namespace ZoneManager.Core.PartMonitors;

public sealed class PartMonitorResolver
{
    private readonly IReadOnlyList<PartMonitorTarget> targets;
    private readonly LayoutMetrics metrics;

    public PartMonitorResolver(IReadOnlyList<PartMonitorTarget> targets, LayoutMetrics metrics)
    {
        this.targets = targets;
        this.metrics = metrics;
    }

    public PartMonitorTarget? FindPhysicalMonitor(PointInt point) =>
        targets.FirstOrDefault(target => target.Monitor.WorkArea.Contains(point));

    public PartMonitorPlacement? FindAt(PointInt point)
    {
        var target = FindPhysicalMonitor(point);
        if (target is null)
        {
            return null;
        }

        var partMonitor = ZoneGeometry.HitTest(
            target.PartMonitors,
            target.Monitor.WorkArea,
            metrics,
            point);
        return partMonitor is null ? null : ToPlacement(target, partMonitor.Id);
    }

    public PartMonitorPlacement? Resolve(string monitorId, Guid partMonitorId)
    {
        var target = targets.FirstOrDefault(candidate =>
            string.Equals(candidate.Monitor.Identity.StableId, monitorId, StringComparison.OrdinalIgnoreCase));
        return target is null ? null : ToPlacement(target, partMonitorId);
    }

    public PartMonitorPlacement? Cycle(
        string currentMonitorId,
        Guid currentPartMonitorId,
        int offset)
    {
        if (offset is not (-1 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var ordered = targets
            .SelectMany(target => target.PartMonitors.Select(partMonitor => (target, partMonitor)))
            .ToArray();
        var current = Array.FindIndex(ordered, item =>
            string.Equals(item.target.Monitor.Identity.StableId, currentMonitorId, StringComparison.OrdinalIgnoreCase) &&
            item.partMonitor.Id == currentPartMonitorId);
        if (current < 0 || ordered.Length == 0)
        {
            return null;
        }

        var destination = (current + offset + ordered.Length) % ordered.Length;
        return ToPlacement(ordered[destination].target, ordered[destination].partMonitor.Id);
    }

    private PartMonitorPlacement? ToPlacement(PartMonitorTarget target, Guid partMonitorId)
    {
        var partMonitor = target.PartMonitors.FirstOrDefault(candidate => candidate.Id == partMonitorId);
        return partMonitor is null
            ? null
            : new PartMonitorPlacement(
                target.Monitor.Identity.StableId,
                partMonitor.Id,
                ZoneGeometry.ToPixels(partMonitor.Bounds, target.Monitor.WorkArea));
    }
}
