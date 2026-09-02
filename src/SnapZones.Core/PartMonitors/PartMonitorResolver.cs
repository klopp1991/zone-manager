using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.Core.PartMonitors;

/// <summary>
/// Loest Zonen zu Bildschirmrechtecken auf. Seit dem 02.09.2026 gilt ueberall dieselbe Geometrie wie im
/// Overlay: Aussenabstand und Zonenabstand aus <see cref="LayoutMetrics"/> wirken auch auf das Fenster.
/// Frueher zeigte die Vorschau Abstaende, das Fenster wurde aber auf die volle Zone gesetzt.
/// </summary>
public sealed class PartMonitorResolver
{
    private readonly IReadOnlyList<PartMonitorTarget> targets;
    private readonly LayoutMetrics metrics;

    public PartMonitorResolver(IReadOnlyList<PartMonitorTarget> targets, LayoutMetrics metrics)
    {
        this.targets = targets;
        this.metrics = metrics;
    }

    /// <summary>Der Monitor, auf dessen Arbeitsflaeche der Punkt liegt; null ueber Taskleiste oder Luecke.</summary>
    public PartMonitorTarget? FindPhysicalMonitor(PointInt point) =>
        targets.FirstOrDefault(target => target.Monitor.WorkArea.Contains(point));

    /// <summary>
    /// Der Monitor, dem der Punkt am naechsten liegt. Ein Ziehvorgang beginnt oft ueber der Taskleiste,
    /// also ausserhalb jeder Arbeitsflaeche; ohne diesen Rueckfall erschien dann gar kein Overlay.
    /// </summary>
    public PartMonitorTarget? FindNearestMonitor(PointInt point)
    {
        if (FindPhysicalMonitor(point) is { } exact)
        {
            return exact;
        }

        PartMonitorTarget? nearest = null;
        var nearestDistance = long.MaxValue;
        foreach (var target in targets)
        {
            var distance = target.Monitor.MonitorBounds.DistanceSquaredTo(point);
            if (distance < nearestDistance)
            {
                nearest = target;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    public PartMonitorPlacement? FindAt(PointInt point)
    {
        var target = FindPhysicalMonitor(point);
        if (target is null)
        {
            return null;
        }

        // Getroffen wird ueber die ungepolsterte Zone: im schmalen Zwischenraum zweier Zonen soll das
        // Loslassen nicht ins Leere gehen. Gesetzt wird das Fenster dann mit den Abstaenden.
        var partMonitor = ZoneGeometry.HitTest(
            target.PartMonitors,
            target.Monitor.WorkArea,
            point);
        return partMonitor is null ? null : ToPlacement(target, partMonitor.Id);
    }

    public PartMonitorPlacement? Resolve(string monitorId, Guid partMonitorId)
    {
        var target = targets.FirstOrDefault(candidate =>
            string.Equals(candidate.Monitor.Identity.StableId, monitorId, StringComparison.OrdinalIgnoreCase));
        return target is null ? null : ToPlacement(target, partMonitorId);
    }

    /// <summary>
    /// Loest mehrere Zonen desselben Monitors zu einer gemeinsamen Zielflaeche auf. Das Ergebnis traegt
    /// die erste noch vorhandene Zone als Kennung, damit Verlauf und Weiterschalten unveraendert
    /// weiterarbeiten. Unbekannte Zonen werden uebergangen; bleibt keine uebrig, ist das Ziel leer.
    /// </summary>
    public PartMonitorPlacement? ResolveSpan(string monitorId, IReadOnlyList<Guid> partMonitorIds)
    {
        ArgumentNullException.ThrowIfNull(partMonitorIds);
        var target = targets.FirstOrDefault(candidate =>
            string.Equals(candidate.Monitor.Identity.StableId, monitorId, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return null;
        }

        PartMonitorPlacement? combined = null;
        foreach (var partMonitorId in partMonitorIds)
        {
            if (ToPlacement(target, partMonitorId) is not { } placement)
            {
                continue;
            }

            combined = combined is null
                ? placement
                : combined with { Bounds = combined.Bounds.Union(placement.Bounds) };
        }

        return combined;
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
                ZoneGeometry.ToPixels(partMonitor.Bounds, target.Monitor.WorkArea, metrics));
    }
}
