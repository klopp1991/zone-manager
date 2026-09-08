using SnapZones.Core.Layouts;
using SnapZones.Core.Models;

namespace SnapZones.Core.Monitors;

/// <summary>
/// Schreibt Monitorschluessel, die noch am Anzeigepfad haengen, auf die Hardwarekennung um. Bis zum
/// 08.09.2026 war der Anzeigepfad der Schluessel fuer Name, Reihenfolge und Monitorkombination; er
/// enthaelt Grafikkarte und Anschluss und wechselt deshalb beim Umstecken. Ein einmal benannter
/// Monitor verlor danach seinen Namen und trat unter der von Windows neu vergebenen Anzeigenummer
/// wieder auf («Monitor 6»), waehrend der alte Name als verwaister Eintrag liegen blieb.
///
/// <para>
/// Die Zuordnung stammt aus den Layouts: dort steht neben dem Anzeigepfad auch die Hardwarekennung.
/// Umgeschrieben wird nur, wo die Kennung eine Seriennummer traegt und damit genau ein Geraet
/// bezeichnet. Der Durchlauf ist wiederholbar; ein bereits umgestellter Stand bleibt unveraendert.
/// </para>
/// </summary>
public static class MonitorKeyMigration
{
    public static SnapConfiguration Apply(SnapConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        // Zwei Eintraege desselben Monitors an zwei Anschluessen werden zu einem. Damit koennen zwei
        // Layouts aktiv sein oder zwei denselben Namen tragen, was beides je Monitor nicht sein darf.
        var layouts = NormalizePerMonitor(configuration.Layouts);
        configuration = ReferenceEquals(layouts, configuration.Layouts)
            ? configuration
            : configuration with { Layouts = layouts };

        var replacements = BuildReplacements(configuration.Layouts);
        if (replacements.Count == 0)
        {
            return configuration;
        }

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in configuration.MonitorNames)
        {
            // Treffen zwei alte Schluessel auf denselben Monitor, gewinnt der erste: es ist derselbe
            // Monitor an zwei Anschluessen, und beide Namen sind gleich gut.
            names.TryAdd(Rewrite(entry.Key, replacements), entry.Value);
        }

        var order = configuration.MonitorOrder
            .Select(key => Rewrite(key, replacements))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sets = configuration.MonitorSets
            .Select(set => RewriteSet(set, replacements))
            .ToArray();

        return configuration with
        {
            MonitorNames = names,
            MonitorOrder = order,
            MonitorSets = MonitorSets.Prune(sets, configuration.Layouts)
        };
    }

    /// <summary>
    /// Stellt in zusammengelegten Monitoren die beiden Regeln her, die eine gespeicherte Konfiguration
    /// einhalten muss: genau ein aktives Layout und eindeutige Layoutnamen. Angefasst werden nur
    /// Monitore, deren Layouts an mehreren Anschluessen aufgezeichnet wurden — sonst waere ein Stand,
    /// der die Regeln von sich aus verletzt, stillschweigend geheilt statt abgelehnt.
    /// </summary>
    private static IReadOnlyList<MonitorLayout> NormalizePerMonitor(IReadOnlyList<MonitorLayout> layouts)
    {
        var normalized = layouts.ToArray();
        var changed = false;
        foreach (var group in normalized
                     .Select((layout, index) => (layout, index))
                     .GroupBy(entry => MonitorNaming.KeyFor(entry.layout.Monitor), StringComparer.OrdinalIgnoreCase))
        {
            var entries = group.ToArray();
            if (entries.Select(entry => entry.layout.Monitor.StableId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count() < 2)
            {
                continue;
            }

            var activeIndex = Array.FindIndex(entries, entry => entry.layout.IsActive);
            if (activeIndex < 0)
            {
                activeIndex = 0;
            }

            var usedNames = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            for (var position = 0; position < entries.Length; position++)
            {
                var (layout, index) = entries[position];
                var shouldBeActive = position == activeIndex;
                var name = UniqueName(layout.Name.Trim(), usedNames);
                if (layout.IsActive == shouldBeActive && string.Equals(layout.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                normalized[index] = layout with { IsActive = shouldBeActive, Name = name };
                changed = true;
            }
        }

        return changed ? normalized : layouts;
    }

    private static string UniqueName(string name, HashSet<string> used)
    {
        if (used.Add(name))
        {
            return name;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{name} ({suffix})";
            if (used.Add(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>Anzeigepfad-Schluessel und der Schluessel, der ihn ersetzt, aus allen Layouts.</summary>
    private static Dictionary<string, string> BuildReplacements(IReadOnlyList<MonitorLayout> layouts)
    {
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layout in layouts)
        {
            var monitor = layout.Monitor;
            if (!MonitorHardwareId.HasSerialNumber(monitor.HardwareId) ||
                string.IsNullOrWhiteSpace(monitor.StableId))
            {
                continue;
            }

            var oldKey = $"stable:{monitor.StableId}";
            var newKey = MonitorNaming.KeyFor(monitor);
            if (!string.Equals(oldKey, newKey, StringComparison.OrdinalIgnoreCase))
            {
                replacements.TryAdd(oldKey, newKey);
            }
        }

        return replacements;
    }

    private static string Rewrite(string key, Dictionary<string, string> replacements) =>
        replacements.TryGetValue(key, out var replacement) ? replacement : key;

    private static MonitorSetSelection RewriteSet(MonitorSetSelection set, Dictionary<string, string> replacements)
    {
        var setKey = string.Join(
            '+',
            set.SetKey.Split('+').Select(part => Rewrite(part, replacements)).Distinct(StringComparer.OrdinalIgnoreCase));
        var active = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in set.ActiveLayouts)
        {
            active.TryAdd(Rewrite(entry.Key, replacements), entry.Value);
        }

        return new MonitorSetSelection(setKey, active);
    }
}
