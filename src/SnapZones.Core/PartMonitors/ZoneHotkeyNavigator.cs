using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.Core.PartMonitors;

/// <summary>Was ein Tastenkuerzel mit dem Vordergrundfenster tun soll.</summary>
public enum ZoneHotkeyAction
{
    /// <summary>Eine Zone zurueck, ueber Monitorgrenzen hinweg.</summary>
    PreviousZone,

    /// <summary>Eine Zone weiter, ueber Monitorgrenzen hinweg.</summary>
    NextZone,

    /// <summary>Die Zone mit der genannten Nummer auf dem Monitor des Fensters.</summary>
    ZoneByNumber,

    /// <summary>Zurueck an die Stelle vor dem letzten Einrasten.</summary>
    RestorePrevious
}

/// <summary>Ein gedruecktes Tastenkuerzel; die Nummer zaehlt nur fuer <see cref="ZoneHotkeyAction.ZoneByNumber"/>.</summary>
public sealed record ZoneHotkey(ZoneHotkeyAction Action, int ZoneNumber = 0);

/// <summary>
/// Uebersetzt ein Tastenkuerzel in einen Zonenbefehl fuer das Fenster, das gerade im Vordergrund ist.
/// Die Befehle selbst gab es laengst (<see cref="PartMonitorResolver.Cycle"/>,
/// <see cref="RestorePreviousPlacementCommand"/>); bis zum 02.09.2026 hing nur keine Taste daran.
/// </summary>
public static class ZoneHotkeyNavigator
{
    public static PartMonitorCommand? Plan(
        ZoneHotkey hotkey,
        nint windowHandle,
        PixelRect windowBounds,
        IReadOnlyList<PartMonitorTarget> targets,
        LayoutMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(hotkey);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(metrics);
        if (windowHandle == 0 || targets.Count == 0)
        {
            return null;
        }

        if (hotkey.Action == ZoneHotkeyAction.RestorePrevious)
        {
            return new RestorePreviousPlacementCommand(windowHandle);
        }

        var monitor = MonitorOf(windowBounds, targets);
        if (monitor is null || monitor.PartMonitors.Count == 0)
        {
            return null;
        }

        var monitorId = monitor.Monitor.Identity.StableId;
        if (hotkey.Action == ZoneHotkeyAction.ZoneByNumber)
        {
            return hotkey.ZoneNumber >= 1 && hotkey.ZoneNumber <= monitor.PartMonitors.Count
                ? new FillPartMonitorCommand(windowHandle, monitorId, monitor.PartMonitors[hotkey.ZoneNumber - 1].Id)
                : null;
        }

        var current = monitor.PartMonitors.FirstOrDefault(zone =>
            windowBounds.IsWithinTolerance(
                ZoneGeometry.ToPixels(zone.Bounds, monitor.Monitor.WorkArea, metrics),
                WindowFrameCompensation.MaximumBorderPixels));
        var offset = hotkey.Action == ZoneHotkeyAction.NextZone ? 1 : -1;
        if (current is not null)
        {
            return new CyclePartMonitorCommand(windowHandle, monitorId, current.Id, offset);
        }

        // Noch in keiner Zone: vorwaerts beginnt bei der ersten, rueckwaerts bei der letzten Zone.
        var start = offset > 0 ? monitor.PartMonitors[0] : monitor.PartMonitors[^1];
        return new FillPartMonitorCommand(windowHandle, monitorId, start.Id);
    }

    private static PartMonitorTarget? MonitorOf(PixelRect windowBounds, IReadOnlyList<PartMonitorTarget> targets)
    {
        var centre = new PointInt(windowBounds.X + windowBounds.Width / 2, windowBounds.Y + windowBounds.Height / 2);
        return targets.FirstOrDefault(target => target.Monitor.MonitorBounds.Contains(centre))
            ?? targets.MinBy(target => target.Monitor.MonitorBounds.DistanceSquaredTo(centre));
    }
}
