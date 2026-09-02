using SnapZones.Core.Layouts;
using SnapZones.Core.Models;

namespace SnapZones.Core.Monitors;

/// <summary>Was die Abstimmung veraendert hat, im Klartext fuer Statuszeile und Protokoll.</summary>
public sealed record MonitorReconciliationResult(SnapConfiguration Configuration, IReadOnlyList<string> Notices)
{
    public bool Changed => Notices.Count > 0;
}

/// <summary>
/// Bringt die gespeicherte Konfiguration mit den gerade verbundenen Monitoren in Einklang. Laeuft beim
/// Start und bei jeder Aenderung der Monitore:
/// <list type="number">
/// <item>Layouts eines Monitors, dessen Anzeigepfad sich geaendert hat (anderer Anschluss, anderer
/// Treiber), werden ueber die Hardwarekennung aus der EDID wiedererkannt und uebernommen. Bis zum
/// 02.09.2026 galt ein umgesteckter Monitor als neuer Monitor, und seine Layouts blieben als
/// «nicht verbunden» liegen.</item>
/// <item>Die Hardwarekennung wird an Layouts nachgetragen, die noch keine tragen.</item>
/// <item>Die gemerkte Monitorgroesse folgt der aktuellen Arbeitsflaeche.</item>
/// <item>Namen und Reihenfolge verwaister Kennungen werden auf den uebernommenen Monitor umgeschrieben
/// oder entfernt.</item>
/// </list>
/// Nie uebernommen wird bei Mehrdeutigkeit: zwei baugleiche Monitore ohne Seriennummer bleiben getrennt.
/// </summary>
public static class MonitorReconciliation
{
    public static MonitorReconciliationResult Reconcile(SnapConfiguration configuration, IReadOnlyList<LiveMonitor> monitors)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(monitors);
        var notices = new List<string>();
        var layouts = configuration.Layouts.ToList();
        var names = new Dictionary<string, string>(configuration.MonitorNames, StringComparer.OrdinalIgnoreCase);
        var order = configuration.MonitorOrder.ToList();
        var sets = configuration.MonitorSets.ToList();

        foreach (var live in monitors)
        {
            var identity = live.Identity;
            if (layouts.Any(layout => LayoutService.BelongsToMonitor(layout.Monitor, identity)))
            {
                RefreshKnownMonitor(layouts, live, notices);
                continue;
            }

            var orphan = FindOrphan(layouts, monitors, live);
            if (orphan is null)
            {
                continue;
            }

            var oldKey = MonitorNaming.KeyFor(orphan);
            var newKey = MonitorNaming.KeyFor(identity);
            for (var index = 0; index < layouts.Count; index++)
            {
                if (LayoutService.BelongsToMonitor(layouts[index].Monitor, orphan))
                {
                    layouts[index] = layouts[index] with
                    {
                        Monitor = identity,
                        SavedWidth = live.WorkArea.Width,
                        SavedHeight = live.WorkArea.Height
                    };
                }
            }

            if (names.Remove(oldKey, out var customName) && !names.ContainsKey(newKey))
            {
                names[newKey] = customName;
            }

            var orderIndex = order.FindIndex(key => string.Equals(key, oldKey, StringComparison.OrdinalIgnoreCase));
            if (orderIndex >= 0)
            {
                order[orderIndex] = newKey;
            }

            sets = sets
                .Select(set => new MonitorSetSelection(
                    set.SetKey,
                    set.ActiveLayouts.ToDictionary(
                        entry => string.Equals(entry.Key, oldKey, StringComparison.OrdinalIgnoreCase) ? newKey : entry.Key,
                        entry => entry.Value,
                        StringComparer.OrdinalIgnoreCase)))
                .ToList();
            var displayName = MonitorNaming.UserFacingName(
                customName,
                MonitorNaming.ResolveDisplayNumber(identity, 1));
            notices.Add($"Layouts von «{displayName}» am neuen Anschluss übernommen.");
        }

        RemoveOrphanedKeys(layouts, monitors, names, order, notices);

        var result = configuration with
        {
            Layouts = layouts,
            MonitorNames = names,
            MonitorOrder = order.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            MonitorSets = MonitorSets.Prune(sets, layouts)
        };
        return new MonitorReconciliationResult(result, notices);
    }

    /// <summary>
    /// Traegt Hardwarekennung und aktuelle Groesse nach. Eine geaenderte Aufloesung ist kein Grund fuer
    /// eine Meldung: die Zonen sind in Prozent definiert und folgen ihr von selbst.
    /// </summary>
    private static void RefreshKnownMonitor(List<MonitorLayout> layouts, LiveMonitor live, List<string> notices)
    {
        var sizeChanged = false;
        for (var index = 0; index < layouts.Count; index++)
        {
            var layout = layouts[index];
            if (!LayoutService.BelongsToMonitor(layout.Monitor, live.Identity))
            {
                continue;
            }

            // Die Kennung aus der EDID (mit Seriennummer) ersetzt eine nur aus dem Anzeigepfad
            // abgeleitete (nur Modell); ein anderes Modell wuerde nie ueberschrieben.
            var monitor = layout.Monitor;
            var liveHardwareId = live.Identity.HardwareId;
            if (!string.IsNullOrWhiteSpace(liveHardwareId) &&
                !string.Equals(monitor.HardwareId, liveHardwareId, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(monitor.HardwareId) ||
                    string.Equals(MonitorHardwareId.ModelOf(monitor.HardwareId), MonitorHardwareId.ModelOf(liveHardwareId), StringComparison.OrdinalIgnoreCase)))
            {
                monitor = monitor with { HardwareId = liveHardwareId };
            }

            if (layout.SavedWidth != live.WorkArea.Width || layout.SavedHeight != live.WorkArea.Height)
            {
                sizeChanged = true;
            }

            layouts[index] = layout with
            {
                Monitor = monitor,
                SavedWidth = live.WorkArea.Width,
                SavedHeight = live.WorkArea.Height
            };
        }

        if (sizeChanged)
        {
            notices.Add($"Arbeitsfläche von «{live.Identity.FriendlyName}» ist jetzt {live.WorkArea.Width} × {live.WorkArea.Height}.");
        }
    }

    /// <summary>
    /// Sucht unter den Layouts nicht verbundener Monitore genau einen, der zur Hardware passt. Zuerst
    /// zaehlt die vollstaendige Kennung mit Seriennummer, dann das Modell allein; beides nur, wenn es
    /// eindeutig ist und kein anderer verbundener Monitor dieselbe Kennung traegt.
    /// </summary>
    private static MonitorIdentity? FindOrphan(List<MonitorLayout> layouts, IReadOnlyList<LiveMonitor> monitors, LiveMonitor live)
    {
        var hardwareId = live.Identity.HardwareId;
        if (string.IsNullOrWhiteSpace(hardwareId))
        {
            return null;
        }

        var orphanIdentities = layouts
            .Select(layout => layout.Monitor)
            .Where(monitor => !monitors.Any(candidate => LayoutService.BelongsToMonitor(candidate.Identity, monitor)))
            .GroupBy(MonitorNaming.KeyFor, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        if (orphanIdentities.Length == 0)
        {
            return null;
        }

        var exact = orphanIdentities
            .Where(monitor => string.Equals(EffectiveHardwareId(monitor), hardwareId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (exact.Length == 1 && CountLive(monitors, hardwareId, exactMatch: true) == 1)
        {
            return exact[0];
        }

        var model = MonitorHardwareId.ModelOf(hardwareId);
        var byModel = orphanIdentities
            .Where(monitor => string.Equals(MonitorHardwareId.ModelOf(EffectiveHardwareId(monitor)), model, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return byModel.Length == 1 && CountLive(monitors, model, exactMatch: false) == 1 ? byModel[0] : null;
    }

    private static int CountLive(IReadOnlyList<LiveMonitor> monitors, string key, bool exactMatch) =>
        monitors.Count(monitor => string.Equals(
            exactMatch ? monitor.Identity.HardwareId : MonitorHardwareId.ModelOf(monitor.Identity.HardwareId),
            key,
            StringComparison.OrdinalIgnoreCase));

    /// <summary>Die Hardwarekennung eines gespeicherten Monitors; notfalls aus dem Anzeigepfad abgeleitet.</summary>
    public static string EffectiveHardwareId(MonitorIdentity monitor) =>
        string.IsNullOrWhiteSpace(monitor.HardwareId)
            ? MonitorHardwareId.FromDevicePath(monitor.StableId)
            : monitor.HardwareId;

    private static void RemoveOrphanedKeys(
        List<MonitorLayout> layouts,
        IReadOnlyList<LiveMonitor> monitors,
        Dictionary<string, string> names,
        List<string> order,
        List<string> notices)
    {
        var referenced = layouts.Select(layout => MonitorNaming.KeyFor(layout.Monitor))
            .Concat(monitors.Select(monitor => MonitorNaming.KeyFor(monitor.Identity)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removedNames = names.Keys.Where(key => !referenced.Contains(key)).ToArray();
        foreach (var key in removedNames)
        {
            names.Remove(key);
        }

        var removedOrder = order.RemoveAll(key => !referenced.Contains(key));
        if (removedNames.Length + removedOrder > 0)
        {
            notices.Add($"{removedNames.Length + removedOrder} verwaiste Monitoreinträge bereinigt.");
        }
    }
}
