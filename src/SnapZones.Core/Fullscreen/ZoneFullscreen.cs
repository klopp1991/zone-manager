using SnapZones.Core.Geometry;
using SnapZones.Core.Placement;

namespace SnapZones.Core.Fullscreen;

/// <summary>
/// Die Geometrie des Zonen-Vollbilds: ob ein Fenster den ganzen Monitor einnimmt und welche Flaeche es
/// stattdessen einnehmen soll.
///
/// <para>
/// Ein Videoplayer im Browser schaltet nicht in den Exklusivmodus der Grafikkarte, sondern setzt sein
/// Fenster randlos auf die volle Monitorflaeche. Das ist ein gewoehnliches Fenster und laesst sich auf
/// eine Zone zuruecksetzen; der Player bleibt dabei in seinem Vollbildzustand und rechnet nur mit einer
/// kleineren Flaeche. Echtes Exklusivvollbild, wie es Spiele ueber DirectX anfordern, vergibt der
/// Grafiktreiber und ist von hier aus nicht erreichbar.
/// </para>
/// </summary>
public static class ZoneFullscreen
{
    /// <summary>
    /// Wie weit das Fensterrechteck von den Monitorkanten abweichen darf und trotzdem als Vollbild gilt.
    /// Manche Programme setzen sich um ein Pixel groesser als der Monitor.
    /// </summary>
    public const int MonitorCoverageTolerancePixels = 2;

    /// <summary>
    /// Ob das Fenster die volle Monitorflaeche einnimmt. Gemessen wird gegen die ganze Monitorflaeche,
    /// nicht gegen die Arbeitsflaeche: ein maximiertes Fenster endet an der Taskleiste und faellt damit
    /// von selbst heraus. Nur wenn die Taskleiste automatisch ausgeblendet wird, decken sich beide
    /// Flaechen — deshalb prueft der Aufrufer zusaetzlich, dass das Fenster nicht maximiert ist.
    /// </summary>
    public static bool CoversMonitor(PixelRect windowBounds, PixelRect monitorBounds) =>
        CoversMonitor(windowBounds, monitorBounds, MonitorCoverageTolerancePixels);

    public static bool CoversMonitor(PixelRect windowBounds, PixelRect monitorBounds, int tolerancePixels)
    {
        if (windowBounds.Width <= 0 || windowBounds.Height <= 0 ||
            monitorBounds.Width <= 0 || monitorBounds.Height <= 0)
        {
            return false;
        }

        var tolerance = Math.Max(0, tolerancePixels);
        return windowBounds.X <= monitorBounds.X + tolerance &&
            windowBounds.Y <= monitorBounds.Y + tolerance &&
            windowBounds.Right >= monitorBounds.Right - tolerance &&
            windowBounds.Bottom >= monitorBounds.Bottom - tolerance;
    }

    /// <summary>
    /// Die Flaeche, auf der das Fenster eingerastet liegt, oder <c>null</c>, wenn es auf keiner liegt.
    ///
    /// <para>
    /// Zuerst zaehlt eine einzelne Zone. Trifft keine, wird der Verbund geprueft: ein mit gedrueckter
    /// Strg-Taste ueber mehrere Zonen gezogenes Fenster belegt deren gemeinsame Flaeche und passt
    /// deshalb auf keine einzelne. Dafuer werden alle Zonen gesammelt, die im Fensterrechteck liegen,
    /// und ihre Huellbox mit dem Fensterrechteck verglichen.
    /// </para>
    ///
    /// <para>
    /// Verglichen wird mit derselben Toleranz wie beim Auffang in der Hauptzone: ein Fenster liegt wegen
    /// des unsichtbaren Griffbereichs nie exakt auf den Zonenraendern.
    /// </para>
    /// </summary>
    public static PixelRect? FindSnappedArea(
        PixelRect windowBounds,
        IReadOnlyList<PlacementZoneTarget> zones,
        int tolerancePixels)
    {
        ArgumentNullException.ThrowIfNull(zones);
        if (windowBounds.Width <= 0 || windowBounds.Height <= 0 || zones.Count == 0)
        {
            return null;
        }

        var tolerance = Math.Max(0, tolerancePixels);
        foreach (var zone in zones)
        {
            if (windowBounds.IsWithinTolerance(zone.Bounds, tolerance))
            {
                return zone.Bounds;
            }
        }

        var covered = new PixelRect(0, 0, 0, 0);
        var count = 0;
        foreach (var zone in zones)
        {
            if (zone.Bounds.Width <= 0 || zone.Bounds.Height <= 0 || !IsInside(zone.Bounds, windowBounds, tolerance))
            {
                continue;
            }

            covered = covered.Union(zone.Bounds);
            count++;
        }

        return count >= 2 && covered.IsWithinTolerance(windowBounds, tolerance) ? covered : null;
    }

    /// <summary>Ob die Zone innerhalb des Fensterrechtecks liegt, mit Toleranz an allen vier Kanten.</summary>
    private static bool IsInside(PixelRect zone, PixelRect windowBounds, int tolerance) =>
        zone.X >= windowBounds.X - tolerance &&
        zone.Y >= windowBounds.Y - tolerance &&
        zone.Right <= windowBounds.Right + tolerance &&
        zone.Bottom <= windowBounds.Bottom + tolerance;
}
