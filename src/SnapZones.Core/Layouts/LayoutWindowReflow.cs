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
        LayoutMetrics oldMetrics,
        LayoutMetrics newMetrics,
        IEnumerable<WindowPlacement> windows)
    {
        ArgumentNullException.ThrowIfNull(oldLayout);
        ArgumentNullException.ThrowIfNull(newLayout);
        ArgumentNullException.ThrowIfNull(windows);

        var newZones = newLayout.Zones.ToDictionary(zone => zone.Id);
        var targets = new List<WindowPlacement>();
        foreach (var window in windows)
        {
            var oldZone = oldLayout.Zones.FirstOrDefault(zone =>
                ZoneGeometry.ToPixels(zone.Bounds, workArea, oldMetrics).Contains(window.Bounds));
            if (oldZone is null || !newZones.TryGetValue(oldZone.Id, out var newZone))
            {
                continue;
            }

            var targetBounds = ZoneGeometry.ToPixels(newZone.Bounds, workArea, newMetrics);
            if (targetBounds != window.Bounds)
            {
                targets.Add(new WindowPlacement(window.WindowHandle, targetBounds));
            }
        }

        return targets;
    }
}
