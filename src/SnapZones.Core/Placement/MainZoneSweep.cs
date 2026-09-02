using SnapZones.Core.AppRules;
using SnapZones.Core.Geometry;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;

namespace SnapZones.Core.Placement;

/// <summary>Ein Fenster, wie es der Auffang beim Layoutwechsel vorfindet.</summary>
public sealed record MainZoneSweepWindow(nint WindowHandle, PixelRect Bounds);

/// <summary>
/// Sammelt nach einem Layoutwechsel die Fenster des betroffenen Monitors ein, die im neuen Layout auf
/// keiner Zone mehr liegen, und legt sie in die Hauptzone.
///
/// <para>
/// Bewusst nur beim tatsächlichen Wechsel des aktiven Layouts: während des Bearbeitens speichert die
/// Oberfläche nach jedem Zug, und ein Auffang bei jedem Speichern würde dem Benutzer die Fenster unter den
/// Händen wegziehen.
/// </para>
/// </summary>
public static class MainZoneSweep
{
    /// <summary>
    /// Die Fenster, die verschoben werden sollen, samt Zielfläche. Leer, wenn die Snap-Funktion aus ist,
    /// keine Hauptzone gilt oder kein Fenster in Frage kommt.
    /// </summary>
    /// <param name="activatedWorkArea">Die Arbeitsfläche des Monitors, dessen Layout gewechselt hat.</param>
    /// <param name="resolveIdentity">
    /// Liefert die Fensteridentität für Ausschluss- und Regelprüfung. Absichtlich als Rückruf, damit die
    /// teure Abfrage nur für Fenster läuft, die die geometrische Prüfung überhaupt bestanden haben.
    /// </param>
    public static IReadOnlyList<WindowPlacement> Plan(
        SnapConfiguration? configuration,
        IReadOnlyList<PlacementZoneTarget> zones,
        MonitorWorkArea activatedWorkArea,
        IEnumerable<MainZoneSweepWindow> windows,
        Func<nint, AppWindowIdentity?> resolveIdentity)
    {
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(resolveIdentity);
        if (configuration is null ||
            !SnapActivationPolicy.ShouldEnable(configuration) ||
            !configuration.Settings.CatchNewWindowsInMainZone ||
            MainZone.Resolve(configuration) is null)
        {
            return [];
        }

        var planned = new List<WindowPlacement>();
        foreach (var window in windows)
        {
            if (!IsCentredOn(window.Bounds, activatedWorkArea) ||
                MainZoneFallback.Resolve(configuration, zones, window.Bounds) is not { } bounds)
            {
                continue;
            }

            // Ein Fenster ohne lesbare Identität bleibt unberührt: ohne sie liesse sich weder ein
            // Ausschluss noch eine Regel prüfen, und beide haben Vorrang vor der Hauptzone.
            var identity = resolveIdentity(window.WindowHandle);
            if (identity is null ||
                AppExclusionMatcher.IsExcluded(configuration.AppExclusions, identity) ||
                configuration.AppRules.Any(rule => rule.IsEnabled && AppRuleMatcher.Matches(rule, identity)))
            {
                continue;
            }

            planned.Add(new WindowPlacement(window.WindowHandle, bounds));
        }

        return planned;
    }

    /// <summary>Ob der Mittelpunkt des Fensters auf dieser Arbeitsfläche liegt.</summary>
    public static bool IsCentredOn(PixelRect bounds, MonitorWorkArea workArea)
    {
        var centreX = bounds.X + (bounds.Width / 2);
        var centreY = bounds.Y + (bounds.Height / 2);
        return centreX >= workArea.X &&
            centreX < workArea.X + workArea.Width &&
            centreY >= workArea.Y &&
            centreY < workArea.Y + workArea.Height;
    }
}
