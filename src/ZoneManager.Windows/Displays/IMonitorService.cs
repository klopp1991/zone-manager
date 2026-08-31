using ZoneManager.Core.Monitors;

namespace ZoneManager.Windows.Displays;

public interface IMonitorService
{
    IReadOnlyList<LiveMonitor> GetMonitors();
}
