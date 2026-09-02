using SnapZones.Core.Models;
using SnapZones.Core.Monitors;

namespace SnapZones.Core.Layouts;

/// <summary>Die aufgelöste Hauptzone: das Layout, dem sie gehört, und die Zone selbst.</summary>
public sealed record MainZoneTarget(MonitorLayout Layout, ZoneDefinition Zone)
{
    /// <summary>Layout und Zone in einer Zeile, so wie es auch die App-Regeln melden.</summary>
    public string DisplayName => $"{Layout.Name} / {Zone.Name}";
}

/// <summary>
/// Die Hauptzone ist die Arbeitszone, in der neu erscheinende Fenster landen, wenn sie sonst nirgends
/// hingehören.
///
/// <para>
/// Jedes Layout darf eine eigene Hauptzone tragen; welche davon zur Laufzeit gilt, entscheidet die
/// Monitorreihenfolge aus den Einstellungen: es gewinnt die Hauptzone des ersten Monitors, dessen aktives
/// Layout überhaupt eine trägt. Damit bleibt der Ort verlässlich derselbe, solange nur eine einzige
/// markiert ist — und ein Layoutwechsel lässt die Hauptzone nicht ausfallen, sobald auch das andere
/// Layout desselben Monitors eine trägt.
/// </para>
/// </summary>
public static class MainZone
{
    /// <summary>
    /// Die gerade gültige Hauptzone, oder <c>null</c>, wenn kein aktives Layout eine trägt. Bei mehreren
    /// gewinnt der in der Monitorreihenfolge vorderste Monitor; Monitore ohne Eintrag in dieser Reihenfolge
    /// stehen hinten, innerhalb eines Monitors entscheidet die Reihenfolge der Layouts.
    /// </summary>
    public static MainZoneTarget? Resolve(SnapConfiguration? configuration)
    {
        if (configuration?.Layouts is null)
        {
            return null;
        }

        var order = configuration.MonitorOrder ?? [];
        return configuration.Layouts
            .Select((layout, index) => (Layout: layout, Index: index))
            .Where(entry => entry.Layout.IsActive && entry.Layout.MainZoneId is not null)
            .Select(entry => (
                entry.Index,
                Rank: MonitorRank(order, entry.Layout.Monitor),
                Target: ToTarget(entry.Layout)))
            .Where(entry => entry.Target is not null)
            .OrderBy(entry => entry.Rank)
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.Target)
            .FirstOrDefault();
    }

    /// <summary>
    /// Setzt die Hauptzone eines Layouts; <c>null</c> hebt sie auf. Andere Layouts bleiben unberührt — sie
    /// dürfen ihre eigene tragen. Eine Zone, die es im Layout nicht gibt, wird abgewiesen, sonst entstünde
    /// ein Verweis ins Leere, den erst die Auflösung zur Laufzeit bemerkt.
    /// </summary>
    public static IReadOnlyList<MonitorLayout> Assign(
        IReadOnlyList<MonitorLayout> layouts,
        Guid layoutId,
        Guid? zoneId)
    {
        ArgumentNullException.ThrowIfNull(layouts);
        var target = layouts.FirstOrDefault(layout => layout.Id == layoutId)
            ?? throw new KeyNotFoundException("Das Layout wurde nicht gefunden.");
        if (zoneId is Guid wanted && target.Zones.All(zone => zone.Id != wanted))
        {
            throw new KeyNotFoundException("Die Zone wurde nicht gefunden.");
        }

        return layouts
            .Select(layout => layout.Id == layoutId ? layout with { MainZoneId = zoneId } : layout)
            .ToArray();
    }

    /// <summary>
    /// Räumt Verweise auf, die durch anderweitige Bearbeitung ungültig geworden sind: eine Hauptzone, die
    /// es in ihrem Layout nicht mehr gibt.
    /// </summary>
    public static IReadOnlyList<MonitorLayout> Normalize(IReadOnlyList<MonitorLayout> layouts)
    {
        ArgumentNullException.ThrowIfNull(layouts);
        return layouts
            .Select(layout => layout.MainZoneId is Guid zoneId && layout.Zones.All(zone => zone.Id != zoneId)
                ? layout with { MainZoneId = null }
                : layout)
            .ToArray();
    }

    private static MainZoneTarget? ToTarget(MonitorLayout layout)
    {
        var zone = layout.Zones.FirstOrDefault(candidate => candidate.Id == layout.MainZoneId);
        return zone is null ? null : new MainZoneTarget(layout, zone);
    }

    private static int MonitorRank(IReadOnlyList<string> order, MonitorIdentity monitor)
    {
        var key = MonitorNaming.KeyFor(monitor);
        for (var index = 0; index < order.Count; index++)
        {
            if (string.Equals(order[index], key, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return int.MaxValue;
    }
}
