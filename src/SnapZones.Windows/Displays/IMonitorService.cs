using SnapZones.Core.Monitors;

namespace SnapZones.Windows.Displays;

public interface IMonitorService
{
    IReadOnlyList<LiveMonitor> GetMonitors();
}
