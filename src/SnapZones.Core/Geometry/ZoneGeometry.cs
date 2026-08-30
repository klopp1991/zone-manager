using SnapZones.Core.Models;

namespace SnapZones.Core.Geometry;

public static class ZoneGeometry
{
    private const double MinimumSize = 0.04;
    private const double Epsilon = 0.0000001;

    public static PixelRect ToPixels(
        NormalizedRect zone,
        MonitorWorkArea area,
        LayoutMetrics metrics)
    {
        var innerWidth = Math.Max(1, area.Width - (2 * Math.Max(0, metrics.OuterMargin)));
        var innerHeight = Math.Max(1, area.Height - (2 * Math.Max(0, metrics.OuterMargin)));
        var gap = Math.Max(0, metrics.ZoneGap);
        var margin = Math.Max(0, metrics.OuterMargin);

        var left = area.X + margin + (int)Math.Round(zone.X * innerWidth)
            + (zone.X > Epsilon ? gap / 2 : 0);
        var top = area.Y + margin + (int)Math.Round(zone.Y * innerHeight)
            + (zone.Y > Epsilon ? gap / 2 : 0);
        var right = area.X + margin + (int)Math.Round((zone.X + zone.Width) * innerWidth)
            - (zone.X + zone.Width < 1 - Epsilon ? gap - (gap / 2) : 0);
        var bottom = area.Y + margin + (int)Math.Round((zone.Y + zone.Height) * innerHeight)
            - (zone.Y + zone.Height < 1 - Epsilon ? gap - (gap / 2) : 0);

        return new PixelRect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    public static ZoneDefinition? HitTest(
        IReadOnlyList<ZoneDefinition> zones,
        MonitorWorkArea area,
        LayoutMetrics metrics,
        PointInt point)
    {
        foreach (var zone in zones)
        {
            if (ToPixels(zone.Bounds, area, metrics).Contains(point))
            {
                return zone;
            }
        }

        return null;
    }

    public static ZoneValidationResult Validate(IReadOnlyList<ZoneDefinition> zones)
    {
        var errors = new List<ZoneValidationError>();
        if (zones.Count == 0)
        {
            errors.Add(new ZoneValidationError("empty", null, "Mindestens eine Zone ist erforderlich."));
            return new ZoneValidationResult(errors);
        }

        foreach (var zone in zones)
        {
            if (string.IsNullOrWhiteSpace(zone.Name))
            {
                errors.Add(new ZoneValidationError("name", zone.Id, "Die Zone benötigt einen Namen."));
            }

            if (!IsFinite(zone.Bounds) || zone.Bounds.Width < MinimumSize || zone.Bounds.Height < MinimumSize)
            {
                errors.Add(new ZoneValidationError("size", zone.Id, "Die Zone ist zu klein oder ungültig."));
            }

            if (zone.Bounds.X < 0 || zone.Bounds.Y < 0 ||
                zone.Bounds.X + zone.Bounds.Width > 1 + Epsilon ||
                zone.Bounds.Y + zone.Bounds.Height > 1 + Epsilon)
            {
                errors.Add(new ZoneValidationError("bounds", zone.Id, "Die Zone liegt ausserhalb des Monitors."));
            }
        }

        for (var first = 0; first < zones.Count; first++)
        {
            for (var second = first + 1; second < zones.Count; second++)
            {
                if (Overlaps(zones[first].Bounds, zones[second].Bounds))
                {
                    errors.Add(new ZoneValidationError(
                        "overlap",
                        zones[second].Id,
                        $"Die Zonen «{zones[first].Name}» und «{zones[second].Name}» überlappen sich."));
                }
            }
        }

        return new ZoneValidationResult(errors);
    }

    private static bool IsFinite(NormalizedRect bounds) =>
        double.IsFinite(bounds.X) && double.IsFinite(bounds.Y) &&
        double.IsFinite(bounds.Width) && double.IsFinite(bounds.Height);

    private static bool Overlaps(NormalizedRect first, NormalizedRect second) =>
        first.X < second.X + second.Width - Epsilon &&
        first.X + first.Width > second.X + Epsilon &&
        first.Y < second.Y + second.Height - Epsilon &&
        first.Y + first.Height > second.Y + Epsilon;
}
