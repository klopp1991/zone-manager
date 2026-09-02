using SnapZones.Core.Geometry;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;

namespace SnapZones.Core.Placement;

/// <summary>
/// Entscheidet, ob ein Fenster in der Hauptzone aufgefangen wird. Der Auffang ist die letzte Stufe der
/// Zuordnungskette: App-Regel, gemerkte Position und Ausschluss haben Vorrang und werden vor dem Aufruf
/// geprüft. Hier bleibt nur noch die Frage, ob das Fenster bereits in einer Zone eingerastet ist.
/// </summary>
public static class MainZoneFallback
{
    /// <summary>
    /// Die Zielfläche der Hauptzone, oder <c>null</c>, wenn keine Hauptzone gilt, sie im aktiven Satz
    /// keine Fläche hat oder das Fenster bereits in einer Zone eingerastet ist.
    /// </summary>
    public static PixelRect? Resolve(
        SnapConfiguration? configuration,
        IReadOnlyList<PlacementZoneTarget> zones,
        PixelRect windowBounds)
    {
        ArgumentNullException.ThrowIfNull(zones);
        if (MainZone.Resolve(configuration) is not { } target)
        {
            return null;
        }

        var zone = zones.FirstOrDefault(candidate => candidate.ZoneId == target.Zone.Id);
        if (zone is null)
        {
            return null;
        }

        // Ein bereits eingerastetes Fenster wird nicht angefasst — auch dann nicht, wenn es in einer
        // anderen Zone liegt als der Hauptzone. Sonst würde die Hauptzone Fenster einsammeln, die der
        // Benutzer bewusst irgendwo abgelegt hat. Ob der Auffang ueberhaupt gewollt ist, entscheidet die
        // Einstellung; die Toleranz fuer «eingerastet» ebenfalls.
        if (!configuration!.Settings.CatchNewWindowsInMainZone)
        {
            return null;
        }

        return IsSnappedToAnyZone(windowBounds, zones, configuration.Settings.SnappedTolerancePixels) ? null : zone.Bounds;
    }

    /// <summary>
    /// Ob das Fenster auf einer der Zonen eingerastet liegt. Verglichen werden die vier Ränder mit einer
    /// Toleranz von <see cref="WindowFrameCompensation.MaximumBorderPixels"/>: genau diesen Betrag kann
    /// der unsichtbare Griffbereich eines Fensters ausmachen, sodass ein eingerastetes Fenster nie exakt
    /// auf den Zonenrändern liegt. Ein bloss überlappendes Fenster gilt bewusst nicht als eingerastet —
    /// bei einem lückenlos gekachelten Monitor überlappt jedes Fenster irgendeine Zone.
    /// </summary>
    public static bool IsSnappedToAnyZone(PixelRect windowBounds, IReadOnlyList<PlacementZoneTarget> zones) =>
        IsSnappedToAnyZone(windowBounds, zones, WindowFrameCompensation.MaximumBorderPixels);

    public static bool IsSnappedToAnyZone(PixelRect windowBounds, IReadOnlyList<PlacementZoneTarget> zones, int tolerancePixels)
    {
        ArgumentNullException.ThrowIfNull(zones);
        return windowBounds.Width > 0 &&
            windowBounds.Height > 0 &&
            zones.Any(zone => windowBounds.IsWithinTolerance(zone.Bounds, tolerancePixels));
    }
}
