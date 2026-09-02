using SnapZones.Core.Models;

namespace SnapZones.Core.Layouts;

/// <summary>Die aufgelöste Hauptzone: das Layout, dem sie gehört, und die Zone selbst.</summary>
public sealed record MainZoneTarget(MonitorLayout Layout, ZoneDefinition Zone)
{
    /// <summary>Layout und Zone in einer Zeile, so wie es auch die App-Regeln melden.</summary>
    public string DisplayName => $"{Layout.Name} / {Zone.Name}";
}

/// <summary>
/// Die Hauptzone ist die Arbeitszone, in der neu erscheinende Fenster landen, wenn sie sonst nirgends
/// hingehören. Es gibt höchstens eine in der gesamten Konfiguration: sie hängt an genau einem Layout
/// und wandert damit beim Layoutwechsel mit. Ist das Layout, das sie trägt, gerade nicht aktiv, gibt es
/// vorübergehend keine Hauptzone, und neue Fenster bleiben unangetastet.
/// </summary>
public static class MainZone
{
    /// <summary>
    /// Die gerade gültige Hauptzone, oder <c>null</c>, wenn keine festgelegt ist, ihr Layout nicht aktiv
    /// ist oder die Zone inzwischen gelöscht wurde.
    /// </summary>
    public static MainZoneTarget? Resolve(SnapConfiguration? configuration)
    {
        if (configuration?.Layouts is null)
        {
            return null;
        }

        foreach (var layout in configuration.Layouts)
        {
            if (!layout.IsActive || layout.MainZoneId is not Guid zoneId)
            {
                continue;
            }

            var zone = layout.Zones.FirstOrDefault(candidate => candidate.Id == zoneId);
            if (zone is not null)
            {
                return new MainZoneTarget(layout, zone);
            }
        }

        return null;
    }

    /// <summary>
    /// Setzt die Hauptzone in einem Layout und nimmt sie überall sonst weg. Ein <c>null</c> als Zone hebt
    /// die Hauptzone auf. Eine Zone, die es im Layout nicht gibt, wird abgewiesen — sonst entstünde ein
    /// Verweis ins Leere, den erst die Auflösung zur Laufzeit bemerkt.
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
            .Select(layout => layout.Id == layoutId
                ? layout with { MainZoneId = zoneId }
                : layout.MainZoneId is null ? layout : layout with { MainZoneId = null })
            .ToArray();
    }

    /// <summary>
    /// Räumt Verweise auf, die durch anderweitige Bearbeitung ungültig geworden sind: eine gelöschte Zone
    /// und mehr als eine Hauptzone. Bleibt mehr als eine übrig, gewinnt die erste in der Layoutliste.
    /// </summary>
    public static IReadOnlyList<MonitorLayout> Normalize(IReadOnlyList<MonitorLayout> layouts)
    {
        ArgumentNullException.ThrowIfNull(layouts);
        var kept = false;
        return layouts
            .Select(layout =>
            {
                if (layout.MainZoneId is not Guid zoneId)
                {
                    return layout;
                }

                if (kept || layout.Zones.All(zone => zone.Id != zoneId))
                {
                    return layout with { MainZoneId = null };
                }

                kept = true;
                return layout;
            })
            .ToArray();
    }
}
