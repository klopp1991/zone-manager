using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using Xunit;

namespace SnapZones.Tests.Monitors;

public sealed class MonitorMatcherTests
{
    [Fact]
    public void Match_prefers_stable_id_when_display_numbers_changed()
    {
        var saved = new[]
        {
            Layout("MONITOR-A", "\\\\.\\DISPLAY1", 3440, 1440),
            Layout("MONITOR-B", "\\\\.\\DISPLAY2", 2560, 1440)
        };
        var live = new[]
        {
            Live("MONITOR-B", "\\\\.\\DISPLAY1", 2560, 1440, false),
            Live("MONITOR-A", "\\\\.\\DISPLAY2", 3440, 1440, true)
        };

        var matches = MonitorMatcher.Match(saved, live);

        Assert.All(matches, match => Assert.Equal(MonitorMatchQuality.StableId, match.Quality));
        Assert.Equal("MONITOR-A", matches[0].Live?.Identity.StableId);
        Assert.Equal("MONITOR-B", matches[1].Live?.Identity.StableId);
    }

    [Fact]
    public void Match_never_assigns_one_live_monitor_twice()
    {
        var saved = new[]
        {
            Layout("UNKNOWN-A", "\\\\.\\DISPLAY9", 1920, 1080),
            Layout("UNKNOWN-B", "\\\\.\\DISPLAY8", 1920, 1080)
        };
        var live = new[] { Live("LIVE", "\\\\.\\DISPLAY1", 1920, 1080, true) };

        var matches = MonitorMatcher.Match(saved, live);

        Assert.Single(matches, match => match.Live is not null);
        Assert.Single(matches, match => match.Quality == MonitorMatchQuality.Missing);
    }

    [Fact]
    public void Match_uses_device_name_before_resolution_fallback()
    {
        var saved = new[] { Layout("OLD", "\\\\.\\DISPLAY3", 1920, 1080) };
        var live = new[]
        {
            Live("OTHER", "\\\\.\\DISPLAY4", 1920, 1080, true),
            Live("REPLACED", "\\\\.\\DISPLAY3", 2560, 1440, false)
        };

        var match = Assert.Single(MonitorMatcher.Match(saved, live));

        Assert.Equal(MonitorMatchQuality.DeviceName, match.Quality);
        Assert.Equal("REPLACED", match.Live?.Identity.StableId);
    }

    private static MonitorLayout Layout(string stableId, string deviceName, int width, int height) =>
        new(new MonitorIdentity(stableId, deviceName, stableId), width, height, []);

    private static LiveMonitor Live(string stableId, string deviceName, int width, int height, bool primary) =>
        new(
            new MonitorIdentity(stableId, deviceName, stableId),
            new MonitorWorkArea(0, 0, width, height),
            96,
            96,
            primary);
}
