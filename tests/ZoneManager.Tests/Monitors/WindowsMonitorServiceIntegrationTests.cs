using ZoneManager.Windows.Displays;
using Xunit;

namespace ZoneManager.Tests.Monitors;

public sealed class WindowsMonitorServiceIntegrationTests
{
    [Fact]
    public void GetMonitors_returns_unique_read_only_display_information()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        var monitors = new WindowsMonitorService().GetMonitors();

        Assert.NotEmpty(monitors);
        Assert.All(monitors, monitor =>
        {
            Assert.False(string.IsNullOrWhiteSpace(monitor.Identity.StableId));
            Assert.True(monitor.WorkArea.Width > 0);
            Assert.True(monitor.WorkArea.Height > 0);
            Assert.True(monitor.DpiX >= 96);
            Assert.True(monitor.DpiY >= 96);
        });
        Assert.Equal(monitors.Count, monitors.Select(monitor => monitor.Identity.StableId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
