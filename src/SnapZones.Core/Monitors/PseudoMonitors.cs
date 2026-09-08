using SnapZones.Core.Layouts;
using SnapZones.Core.Models;

namespace SnapZones.Core.Monitors;

/// <summary>
/// Windows meldet Anzeigen, die fuer Zone Manager kein Monitor sind: <c>WinDisc</c>, solange die
/// Sitzung gesperrt oder per Fernzugriff getrennt ist, <c>Default_Monitor</c> («Generic PnP Monitor»),
/// solange alle echten Monitore aus oder im Standby sind, und den eigenen virtuellen Monitor aus
/// <see cref="VirtualDisplay"/>, den das Programm fuer das Vollbild in einer Zone anlegt. Bis zum
/// 05.09.2026 legte das Programm fuer die Platzhalter ein Layout an; sie blieben danach als «nicht
/// verbundene» Monitore in der Liste stehen und kamen bei jedem Sperren wieder. Diese Klasse erkennt
/// alle drei, damit sie weder eingelesen noch gespeichert werden.
/// </summary>
public static class PseudoMonitors
{
    private const string WinDisc = "WinDisc";
    private const string DefaultMonitorPath = "Default_Monitor";
    private const string DefaultMonitorHardwareId = "DEFAULT_MONITOR";

    /// <summary>Ob diese Kennung eine Platzhalteranzeige von Windows oder den virtuellen Monitor bezeichnet.</summary>
    public static bool IsPseudo(MonitorIdentity monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        return Equals(monitor.StableId, WinDisc) ||
            Equals(monitor.DeviceName, WinDisc) ||
            Equals(monitor.FriendlyName, WinDisc) ||
            Equals(monitor.HardwareId, DefaultMonitorHardwareId) ||
            (monitor.StableId?.Contains(DefaultMonitorPath, StringComparison.OrdinalIgnoreCase) ?? false) ||
            VirtualDisplay.IsVirtual(monitor);
    }

    /// <summary>Nur die echten Monitore einer Liste.</summary>
    public static IReadOnlyList<LiveMonitor> RealOnly(IReadOnlyList<LiveMonitor> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        return monitors.Where(monitor => !IsPseudo(monitor.Identity)).ToArray();
    }

    /// <summary>
    /// Entfernt Layouts, Namen, Reihenfolgeeintraege und Monitorkombinationen der Platzhalteranzeigen
    /// und des virtuellen Monitors aus einer gespeicherten Konfiguration. Liefert dieselbe Instanz,
    /// wenn nichts zu entfernen war.
    /// </summary>
    public static SnapConfiguration Prune(SnapConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var pseudoLayouts = configuration.Layouts.Where(layout => IsPseudo(layout.Monitor)).ToArray();
        var pseudoKeys = pseudoLayouts
            .Select(layout => MonitorNaming.KeyFor(layout.Monitor))
            .Concat([$"stable:{WinDisc}", $"hw:{DefaultMonitorHardwareId}"])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = configuration.MonitorNames.Where(entry => !IsPseudoKey(entry.Key, pseudoKeys)).ToArray();
        var order = configuration.MonitorOrder.Where(key => !IsPseudoKey(key, pseudoKeys)).ToArray();
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

    private static bool IsPseudoKey(string key, HashSet<string> pseudoKeys) =>
        pseudoKeys.Contains(key) || VirtualDisplay.MentionsVirtual(key);

    private static bool SetMentionsPseudo(MonitorSetSelection set, HashSet<string> pseudoKeys) =>
        set.SetKey.Split('+').Any(part => pseudoKeys.Contains(part) ||
            part.Contains(WinDisc, StringComparison.OrdinalIgnoreCase) ||
            part.Contains(DefaultMonitorHardwareId, StringComparison.OrdinalIgnoreCase) ||
            part.Contains(DefaultMonitorPath, StringComparison.OrdinalIgnoreCase) ||
            VirtualDisplay.MentionsVirtual(part)) ||
        set.ActiveLayouts.Keys.Any(key => IsPseudoKey(key, pseudoKeys));

    private static bool Equals(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}
