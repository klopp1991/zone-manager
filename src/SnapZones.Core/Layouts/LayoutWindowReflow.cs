using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.Core.Layouts;

public sealed record WindowPlacement(nint WindowHandle, PixelRect Bounds);

public static class LayoutWindowReflow
{
    public static IReadOnlyList<WindowPlacement> Plan(
        MonitorLayout oldLayout,
        MonitorLayout newLayout,
        MonitorWorkArea workArea,
        IEnumerable<WindowPlacement> windows) =>
        Plan(oldLayout, newLayout, workArea, new LayoutMetrics(0, 0), windows);

    /// <summary>
    /// Fuehrt Fenster mit, deren Zone sich beim Bearbeiten verschoben hat. Ein Fenster gilt als in einer
    /// Zone liegend, wenn seine vier Kanten innerhalb der Toleranz des unsichtbaren Fensterrands auf den
    /// Zonenkanten liegen. Frueher musste das Fensterrechteck vollstaendig in der Zone liegen; wegen des
    /// Griffbereichs von bis zu 13 Pixeln war das fast nie der Fall, und viele Fenster blieben stehen.
    /// </summary>
    public static IReadOnlyList<WindowPlacement> Plan(
        MonitorLayout oldLayout,
        MonitorLayout newLayout,
        MonitorWorkArea workArea,
        LayoutMetrics metrics,
        IEnumerable<WindowPlacement> windows)
    {
        ArgumentNullException.ThrowIfNull(oldLayout);
        ArgumentNullException.ThrowIfNull(newLayout);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(windows);

        var newZones = newLayout.Zones.ToDictionary(zone => zone.Id);
        var targets = new List<WindowPlacement>();
        foreach (var window in windows)
        {
            var oldZone = oldLayout.Zones.FirstOrDefault(zone =>
                window.Bounds.IsWithinTolerance(
                    ZoneGeometry.ToPixels(zone.Bounds, workArea, metrics),
                    WindowFrameCompensation.MaximumBorderPixels));
            if (oldZone is null || !newZones.TryGetValue(oldZone.Id, out var newZone))
            {
                continue;
            }

            var targetBounds = ZoneGeometry.ToPixels(newZone.Bounds, workArea, metrics);
            if (!window.Bounds.IsWithinTolerance(targetBounds, WindowFrameCompensation.MaximumBorderPixels))
            {
                targets.Add(new WindowPlacement(window.WindowHandle, targetBounds));
            }
        }

        return targets;
    }
}
