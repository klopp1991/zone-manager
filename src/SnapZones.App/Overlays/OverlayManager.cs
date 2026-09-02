using SnapZones.Core.Models;
using SnapZones.Core.PartMonitors;

namespace SnapZones.App.Overlays;

public sealed class OverlayManager : IDisposable
{
    private readonly Dictionary<string, MonitorOverlayWindow> windows = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, PartMonitorTarget> targets =
        new Dictionary<string, PartMonitorTarget>();

    public void UpdateTargets(IReadOnlyList<PartMonitorTarget> newTargets)
    {
        targets = newTargets.ToDictionary(target => target.Monitor.Identity.StableId, StringComparer.OrdinalIgnoreCase);
        foreach (var obsolete in windows.Keys.Where(key => !targets.ContainsKey(key)).ToArray())
        {
            windows[obsolete].Close();
            windows.Remove(obsolete);
        }
    }

    public void Show(
        IReadOnlyList<string> monitorIds,
        LayoutMetrics metrics,
        string colour,
        double opacity,
        bool showZoneNames) => Show(monitorIds, metrics, colour, opacity, showZoneNames, OverlayStyle.Default);

    public void Show(
        IReadOnlyList<string> monitorIds,
        LayoutMetrics metrics,
        string colour,
        double opacity,
        bool showZoneNames,
        OverlayStyle style)
    {
        var visible = monitorIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var monitorId in visible)
        {
            if (!targets.TryGetValue(monitorId, out var target))
            {
                continue;
            }

            if (!windows.TryGetValue(monitorId, out var window))
            {
                window = new MonitorOverlayWindow();
                windows.Add(monitorId, window);
            }

            window.ShowFor(target, metrics, colour, opacity, showZoneNames, style);
        }

        foreach (var pair in windows.Where(pair => !visible.Contains(pair.Key)))
        {
            pair.Value.Hide();
        }
    }

    public void Highlight(string? monitorId, Guid? zoneId) =>
        Highlight(monitorId, zoneId is { } id ? [id] : []);

    public void Highlight(string? monitorId, IReadOnlyList<Guid> zoneIds)
    {
        ArgumentNullException.ThrowIfNull(zoneIds);
        foreach (var pair in windows)
        {
            pair.Value.Highlight(
                string.Equals(pair.Key, monitorId, StringComparison.OrdinalIgnoreCase)
                    ? zoneIds
                    : []);
        }
    }

    /// <summary>Ob gerade irgendein Overlay sichtbar ist; steuert die Anzeigeverzoegerung.</summary>
    public bool IsAnyVisible => windows.Values.Any(window => window.IsVisible);

    public void HideAll()
    {
        foreach (var window in windows.Values)
        {
            window.Hide();
        }
    }

    public void Dispose()
    {
        foreach (var window in windows.Values)
        {
            window.Close();
        }

        windows.Clear();
    }
}
