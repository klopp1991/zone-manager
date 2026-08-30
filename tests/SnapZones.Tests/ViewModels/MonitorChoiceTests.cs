using SnapZones.App.ViewModels;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using Xunit;

namespace SnapZones.Tests.ViewModels;

public sealed class MonitorChoiceTests
{
    [Fact]
    public void ResolutionText_contains_only_monitor_resolution()
    {
        var identity = new MonitorIdentity("MONITOR-A", "\\\\.\\DISPLAY1", "Dell U3425WE");
        var liveMonitor = new LiveMonitor(identity, new MonitorWorkArea(0, 0, 3440, 1440), 144, 144, true);
        var layout = new MonitorLayout(identity, 3440, 1440, []);

        var monitorChoice = new MonitorChoice(liveMonitor, layout);

        Assert.Equal("3440 × 1440", monitorChoice.ResolutionText);
    }

    [Fact]
    public void LayoutSuggestions_include_resolution_scaling_and_physical_size()
    {
        var identity = new MonitorIdentity("MONITOR-B", "\\\\.\\DISPLAY2", "Kleines 4K-Display");
        var liveMonitor = new LiveMonitor(
            identity,
            new MonitorWorkArea(0, 0, 3840, 2160),
            96,
            96,
            false,
            30,
            17);
        var layout = new MonitorLayout(identity, 3840, 2160, []);

        var monitorChoice = new MonitorChoice(liveMonitor, layout);

        Assert.NotEmpty(monitorChoice.LayoutSuggestions);
        Assert.All(monitorChoice.LayoutSuggestions, suggestion => Assert.True(suggestion.Zones.Count <= 2));
    }
}
