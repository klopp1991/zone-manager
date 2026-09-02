using SnapZones.Core.Models;
using SnapZones.Core.Monitors;

namespace SnapZones.Core.Layouts;

/// <summary>
/// Merkt sich je Monitorkombination, welche Layouts aktiv waren, und stellt sie beim Wiedersehen der
/// Kombination her. Am Dock will man auf dem Laptopdisplay ein anderes Layout als unterwegs; bis zum
/// 02.09.2026 galt ein einziges aktives Layout je Monitor, und jedes Andocken verlangte Handarbeit.
/// </summary>
public static class MonitorSets
{
    /// <summary>
    /// Der Schluessel einer Kombination: die sortierten Hardwarekennungen der verbundenen Monitore,
    /// ersatzweise ihre Anzeigepfade. Die Reihenfolge der Monitore spielt keine Rolle.
    /// </summary>
    public static string KeyFor(IEnumerable<LiveMonitor> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        var keys = monitors
            .Select(monitor => string.IsNullOrWhiteSpace(monitor.Identity.HardwareId)
                ? MonitorNaming.KeyFor(monitor.Identity)
                : "hw:" + monitor.Identity.HardwareId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase);
        return string.Join("+", keys);
    }

    /// <summary>Haelt die gerade aktiven Layouts der verbundenen Monitore unter dem Schluessel fest.</summary>
    public static SnapConfiguration Record(
        SnapConfiguration configuration,
        string setKey,
        IEnumerable<LiveMonitor> monitors)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(monitors);
        if (string.IsNullOrWhiteSpace(setKey))
        {
            return configuration;
        }

        var active = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var monitor in monitors)
        {
            var layout = configuration.Layouts.FirstOrDefault(candidate =>
                candidate.IsActive && LayoutService.BelongsToMonitor(candidate.Monitor, monitor.Identity));
            if (layout is not null)
            {
                active[MonitorNaming.KeyFor(monitor.Identity)] = layout.Id;
            }
        }

        if (active.Count == 0)
        {
            return configuration;
        }

        var existing = configuration.MonitorSets.FirstOrDefault(set =>
            string.Equals(set.SetKey, setKey, StringComparison.OrdinalIgnoreCase));
        if (existing is not null && SameSelection(existing.ActiveLayouts, active))
        {
            return configuration;
        }

        var sets = configuration.MonitorSets
            .Where(set => !string.Equals(set.SetKey, setKey, StringComparison.OrdinalIgnoreCase))
            .Append(new MonitorSetSelection(setKey, active))
            .ToArray();
        return configuration with { MonitorSets = sets };
    }

    /// <summary>
    /// Aktiviert die fuer die Kombination gemerkten Layouts. Unbekannte Layouts oder Monitore werden
    /// uebergangen; ohne gemerkte Auswahl bleibt alles, wie es ist.
    /// </summary>
    public static SnapConfiguration Apply(
        SnapConfiguration configuration,
        string setKey,
        IEnumerable<LiveMonitor> monitors,
        out IReadOnlyList<MonitorLayout> activated)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(monitors);
        var changed = new List<MonitorLayout>();
        activated = changed;
        var selection = configuration.MonitorSets.FirstOrDefault(set =>
            string.Equals(set.SetKey, setKey, StringComparison.OrdinalIgnoreCase));
        if (selection is null)
        {
            return configuration;
        }

        var layouts = configuration.Layouts.ToArray();
        foreach (var monitor in monitors)
        {
            if (!selection.ActiveLayouts.TryGetValue(MonitorNaming.KeyFor(monitor.Identity), out var layoutId))
            {
                continue;
            }

            var wanted = layouts.FirstOrDefault(layout =>
                layout.Id == layoutId && LayoutService.BelongsToMonitor(layout.Monitor, monitor.Identity));
            if (wanted is null || wanted.IsActive)
            {
                continue;
            }

            for (var index = 0; index < layouts.Length; index++)
            {
                if (LayoutService.BelongsToMonitor(layouts[index].Monitor, monitor.Identity))
                {
                    layouts[index] = layouts[index] with { IsActive = layouts[index].Id == layoutId };
                }
            }

            changed.Add(wanted with { IsActive = true });
        }

        return changed.Count == 0 ? configuration : configuration with { Layouts = layouts };
    }

    /// <summary>Entfernt Verweise auf Layouts, die es nicht mehr gibt, und leere Eintraege.</summary>
    public static IReadOnlyList<MonitorSetSelection> Prune(
        IReadOnlyList<MonitorSetSelection>? sets,
        IReadOnlyList<MonitorLayout> layouts)
    {
        ArgumentNullException.ThrowIfNull(layouts);
        var known = layouts.Select(layout => layout.Id).ToHashSet();
        return (sets ?? [])
            .Where(set => !string.IsNullOrWhiteSpace(set.SetKey) && set.ActiveLayouts is not null)
            .Select(set => new MonitorSetSelection(
                set.SetKey,
                set.ActiveLayouts
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && known.Contains(entry.Value))
                    .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase)))
            .Where(set => set.ActiveLayouts.Count > 0)
            .GroupBy(set => set.SetKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToArray();
    }

    private static bool SameSelection(IReadOnlyDictionary<string, Guid> first, IReadOnlyDictionary<string, Guid> second) =>
        first.Count == second.Count &&
        first.All(entry => second.TryGetValue(entry.Key, out var id) && id == entry.Value);
}
