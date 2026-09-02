namespace SnapZones.Core.Models;

/// <summary>
/// Welche Layouts bei einer bestimmten Monitorkombination aktiv waren. Der Schluessel ist die sortierte
/// Liste der Monitorkennungen; die Werte ordnen jedem Monitor (nach <see cref="Monitors.MonitorNaming.KeyFor"/>)
/// das zuletzt aktive Layout zu. Beim Andocken oder Abstecken wird die zur Kombination gehoerende
/// Auswahl wieder hergestellt, ohne dass jemand von Hand umschalten muss.
/// </summary>
public sealed record MonitorSetSelection(
    string SetKey,
    IReadOnlyDictionary<string, Guid> ActiveLayouts);
