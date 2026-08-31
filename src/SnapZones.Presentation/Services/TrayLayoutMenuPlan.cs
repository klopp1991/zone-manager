using SnapZones.Core.Models;
using SnapZones.Core.Monitors;

namespace SnapZones.Presentation.Services;

public sealed record TrayLayoutMenuItem(Guid Id, string Name, bool IsActive);

public sealed record TrayMonitorMenu(string Name, IReadOnlyList<TrayLayoutMenuItem> Layouts);

public sealed record TrayLayoutMenuPlan(IReadOnlyList<TrayMonitorMenu> Monitors)
{
    public static TrayLayoutMenuPlan Build(SnapConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var monitors = configuration.Layouts
            .GroupBy(MonitorKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => MonitorOrderIndex(configuration.MonitorOrder, MonitorKey(group.First())))
            .ThenBy(group => MonitorNaming.ResolveDisplayNumber(group.First().Monitor, int.MaxValue))
            .Select(group => new TrayMonitorMenu(
                MonitorNaming.UserFacingName(
                    MonitorNaming.CustomNameFor(configuration, group.First().Monitor),
                    MonitorNaming.ResolveDisplayNumber(group.First().Monitor, 1)),
                group.Select(layout => new TrayLayoutMenuItem(layout.Id, layout.Name, layout.IsActive)).ToArray()))
            .ToArray();
        return new TrayLayoutMenuPlan(monitors);
    }

    private static string MonitorKey(MonitorLayout layout) =>
        !string.IsNullOrWhiteSpace(layout.Monitor.StableId)
            ? $"stable:{layout.Monitor.StableId}"
            : $"device:{layout.Monitor.DeviceName}";

    private static int MonitorOrderIndex(IReadOnlyList<string> monitorOrder, string monitorKey)
    {
        for (var index = 0; index < monitorOrder.Count; index++)
        {
            if (string.Equals(monitorOrder[index], monitorKey, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return int.MaxValue;
    }
}
