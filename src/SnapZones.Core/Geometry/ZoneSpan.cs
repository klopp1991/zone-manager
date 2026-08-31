using SnapZones.Core.Models;

namespace SnapZones.Core.Geometry;

/// <summary>
/// Combines several zones of one monitor into the single rectangle a window is
/// placed in when the user spans a drag across them.
/// </summary>
public static class ZoneSpan
{
    /// <summary>
    /// The smallest rectangle that contains all the given zones.
    /// <para>
    /// The zones do not have to touch. Spanning two opposite corners therefore
    /// covers everything in between, which is what makes a span predictable:
    /// the result is always one rectangle, never an L shape.
    /// </para>
    /// </summary>
    /// <param name="zones">Zones to combine. Must not be empty.</param>
    /// <param name="area">Work area the zones are defined against.</param>
    public static PixelRect BoundingBox(IReadOnlyList<ZoneDefinition> zones, MonitorWorkArea area)
    {
        ArgumentNullException.ThrowIfNull(zones);
        if (zones.Count == 0)
        {
            throw new ArgumentException("At least one zone is required.", nameof(zones));
        }

        var left = int.MaxValue;
        var top = int.MaxValue;
        var right = int.MinValue;
        var bottom = int.MinValue;

        foreach (var zone in zones)
        {
            var pixels = ZoneGeometry.ToPixels(zone.Bounds, area);
            left = Math.Min(left, pixels.X);
            top = Math.Min(top, pixels.Y);
            right = Math.Max(right, pixels.X + pixels.Width);
            bottom = Math.Max(bottom, pixels.Y + pixels.Height);
        }

        return new PixelRect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    /// <summary>
    /// Name shown for a span in status messages, for example
    /// "Links + Oben rechts".
    /// </summary>
    public static string Describe(IReadOnlyList<ZoneDefinition> zones)
    {
        ArgumentNullException.ThrowIfNull(zones);
        return string.Join(" + ", zones.Select(zone => zone.Name));
    }
}
