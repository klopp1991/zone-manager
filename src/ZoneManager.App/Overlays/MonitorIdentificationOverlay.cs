using ZoneManager.Core.Monitors;

namespace ZoneManager.App.Overlays;

public sealed record MonitorIdentificationTarget(LiveMonitor Monitor, string Label);

public sealed class MonitorIdentificationOverlay : IDisposable
{
    private readonly Dictionary<string, MonitorIdentificationWindow> windows =
        new(StringComparer.OrdinalIgnoreCase);

    public void Show(IEnumerable<MonitorIdentificationTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var visibleKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in targets)
        {
            var key = MonitorNaming.KeyFor(target.Monitor.Identity);
            visibleKeys.Add(key);
            if (!windows.TryGetValue(key, out var window))
            {
                window = new MonitorIdentificationWindow();
                windows.Add(key, window);
            }

            window.ShowFor(target.Monitor, target.Label);
        }

        foreach (var hidden in windows.Where(entry => !visibleKeys.Contains(entry.Key)))
        {
            hidden.Value.Hide();
        }
    }

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
