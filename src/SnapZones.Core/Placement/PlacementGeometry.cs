using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.Core.Placement;

public static class PlacementGeometry
{
    /// <summary>Untergrenze fuer eine wiederhergestellte Groesse, wenn die gemerkte unbrauchbar ist.</summary>
    private const int FallbackWidth = 160;
    private const int FallbackHeight = 120;

    public static NormalizedRect Normalize(PixelRect bounds, MonitorWorkArea workArea)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workArea), "Die Arbeitsfläche muss positiv sein.");
        }

        return new NormalizedRect(
            (double)(bounds.X - workArea.X) / workArea.Width,
            (double)(bounds.Y - workArea.Y) / workArea.Height,
            (double)bounds.Width / workArea.Width,
            (double)bounds.Height / workArea.Height);
    }

    /// <summary>
    /// Das Zielrechteck fuer ein gemerktes Fenster. Ist die gemerkte Zone im aktiven Satz noch vorhanden,
    /// gilt deren heutige Flaeche: das Fenster kehrt in seine Zone zurueck, nicht an die alten Pixel.
    /// Erst ohne Zone zaehlt die gemerkte Lage, anteilig umgerechnet, wenn sich die Arbeitsflaeche
    /// geaendert hat. Bis zum 02.09.2026 diente die Zone nur zur Monitorsuche.
    /// </summary>
    public static PixelRect Resolve(
        WindowPlacementEntry entry,
        IReadOnlyList<PlacementMonitorTarget> monitors,
        IReadOnlyList<PlacementZoneTarget> zones)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(monitors);
        ArgumentNullException.ThrowIfNull(zones);
        if (monitors.Count == 0)
        {
            throw new ArgumentException("Mindestens ein Monitor ist erforderlich.", nameof(monitors));
        }

        var savedZone = entry.ZoneId is Guid zoneId
            ? zones.FirstOrDefault(zone => zone.ZoneId == zoneId)
            : null;
        if (savedZone is not null &&
            monitors.Any(candidate => candidate.StableId == savedZone.MonitorStableId))
        {
            return savedZone.Bounds;
        }

        var monitor = monitors.FirstOrDefault(candidate => candidate.StableId == entry.MonitorStableId)
            ?? monitors.FirstOrDefault(candidate => candidate.IsPrimary)
            ?? monitors[0];
        var workArea = monitor.WorkArea;
        var bounds = entry.SourceWorkArea == workArea
            ? entry.NormalBoundsPixels
            : Map(entry.NormalBoundsNormalized, workArea);

        // Eine kleine gemerkte Groesse bleibt klein: Taschenrechner und Hilfsfenster wurden frueher beim
        // Wiederherstellen auf 160×120 aufgeblasen. Nur eine unbrauchbare Groesse wird ersetzt.
        var width = Math.Clamp(bounds.Width > 0 ? bounds.Width : FallbackWidth, 1, Math.Max(1, workArea.Width));
        var height = Math.Clamp(bounds.Height > 0 ? bounds.Height : FallbackHeight, 1, Math.Max(1, workArea.Height));
        var x = Math.Clamp(bounds.X, workArea.X, workArea.X + workArea.Width - width);
        var y = Math.Clamp(bounds.Y, workArea.Y, workArea.Y + workArea.Height - height);
        return new PixelRect(x, y, width, height);
    }

    public static Guid? ClassifyZone(PixelRect bounds, IReadOnlyList<PlacementZoneTarget> zones)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || zones.Count == 0)
        {
            return null;
        }

        var windowArea = (long)bounds.Width * bounds.Height;
        var bestArea = 0L;
        Guid? bestZone = null;
        var tied = false;
        foreach (var zone in zones)
        {
            var boundsRight = (long)bounds.X + bounds.Width;
            var boundsBottom = (long)bounds.Y + bounds.Height;
            var zoneRight = (long)zone.Bounds.X + zone.Bounds.Width;
            var zoneBottom = (long)zone.Bounds.Y + zone.Bounds.Height;
            var overlapWidth = Math.Max(0L, Math.Min(boundsRight, zoneRight) - Math.Max((long)bounds.X, zone.Bounds.X));
            var overlapHeight = Math.Max(0L, Math.Min(boundsBottom, zoneBottom) - Math.Max((long)bounds.Y, zone.Bounds.Y));
            var overlapArea = (long)overlapWidth * overlapHeight;
            var minimumArea = windowArea / 4 + (windowArea % 4 == 0 ? 0 : 1);
            if (overlapArea < minimumArea)
            {
                continue;
            }

            if (bestZone is null || overlapArea > bestArea)
            {
                bestArea = overlapArea;
                bestZone = zone.ZoneId;
                tied = false;
            }
            else if (overlapArea == bestArea)
            {
                tied = true;
            }
        }

        return tied ? null : bestZone;
    }

    private static PixelRect Map(NormalizedRect bounds, MonitorWorkArea workArea) => new(
        workArea.X + (int)Math.Round(bounds.X * workArea.Width),
        workArea.Y + (int)Math.Round(bounds.Y * workArea.Height),
        (int)Math.Round(bounds.Width * workArea.Width),
        (int)Math.Round(bounds.Height * workArea.Height));
}
