using SnapZones.Core.Layouts;
using SnapZones.Core.Models;

namespace SnapZones.Core.Monitors;

/// <summary>
/// Windows meldet in zwei Lagen Anzeigen, die kein Monitor sind: <c>WinDisc</c>, solange die Sitzung
/// gesperrt oder per Fernzugriff getrennt ist, und <c>Default_Monitor</c> («Generic PnP Monitor»),
/// solange alle echten Monitore aus oder im Standby sind. Bis zum 05.09.2026 legte das Programm fuer
/// beide ein Layout an; sie blieben danach als «nicht verbundene» Monitore in der Liste stehen und kamen
/// bei jedem Sperren wieder. Diese Klasse erkennt sie, damit sie weder eingelesen noch gespeichert werden.
/// </summary>
public static class PseudoMonitors
{
    private const string WinDisc = "WinDisc";
    private const string DefaultMonitorPath = "Default_Monitor";
    private const string DefaultMonitorHardwareId = "DEFAULT_MONITOR";

    /// <summary>Ob diese Kennung eine Platzhalteranzeige von Windows bezeichnet und kein Monitor ist.</summary>
    public static bool IsPseudo(MonitorIdentity monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        return Equals(monitor.StableId, WinDisc) ||
            Equals(monitor.DeviceName, WinDisc) ||
            Equals(monitor.FriendlyName, WinDisc) ||
            Equals(monitor.HardwareId, DefaultMonitorHardwareId) ||
            (monitor.StableId?.Contains(DefaultMonitorPath, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <summary>Nur die echten Monitore einer Liste.</summary>
    public static IReadOnlyList<LiveMonitor> RealOnly(IReadOnlyList<LiveMonitor> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        return monitors.Where(monitor => !IsPseudo(monitor.Identity)).ToArray();
    }

    /// <summary>
    /// Entfernt Layouts, Namen, Reihenfolgeeintraege und Monitorkombinationen der Platzhalteranzeigen aus
    /// einer gespeicherten Konfiguration. Liefert dieselbe Instanz, wenn nichts zu entfernen war.
    /// </summary>
    public static SnapConfiguration Prune(SnapConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var pseudoLayouts = configuration.Layouts.Where(layout => IsPseudo(layout.Monitor)).ToArray();
        var pseudoKeys = pseudoLayouts
            .Select(layout => MonitorNaming.KeyFor(layout.Monitor))
            .Concat([$"stable:{WinDisc}", $"hw:{DefaultMonitorHardwareId}"])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = configuration.MonitorNames.Where(entry => !pseudoKeys.Contains(entry.Key)).ToArray();
        var order = configuration.MonitorOrder.Where(key => !pseudoKeys.Contains(key)).ToArray();
        var sets = configuration.MonitorSets
            .Where(set => !SetMentionsPseudo(set, pseudoKeys))
            .ToArray();
        if (pseudoLayouts.Length == 0 &&
            names.Length == configuration.MonitorNames.Count &&
            order.Length == configuration.MonitorOrder.Count &&
            sets.Length == configuration.MonitorSets.Count)
        {
            return configuration;
        }

        var layouts = configuration.Layouts.Where(layout => !IsPseudo(layout.Monitor)).ToArray();
        return configuration with
        {
            Layouts = layouts,
            MonitorNames = names.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase),
            MonitorOrder = order,
            MonitorSets = MonitorSets.Prune(sets, layouts)
        };
    }

    private static bool SetMentionsPseudo(MonitorSetSelection set, HashSet<string> pseudoKeys) =>
        set.SetKey.Split('+').Any(part => pseudoKeys.Contains(part) ||
            part.Contains(WinDisc, StringComparison.OrdinalIgnoreCase) ||
            part.Contains(DefaultMonitorHardwareId, StringComparison.OrdinalIgnoreCase) ||
            part.Contains(DefaultMonitorPath, StringComparison.OrdinalIgnoreCase)) ||
        set.ActiveLayouts.Keys.Any(pseudoKeys.Contains);

    private static bool Equals(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}
