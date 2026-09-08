using SnapZones.Core.Geometry;
using Xunit;

namespace SnapZones.Tests.Geometry;

public sealed class MonitorCoverageTests
{
    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    public void Fullscreen_state_uses_the_same_rules_even_when_the_taskbar_is_hidden(
        bool caption, bool minimized, bool maximized, bool expected)
    {
        var bounds = new PixelRect(-2560, -300, 2560, 1440);
        Assert.Equal(expected, MonitorCoverage.IsFullscreen(bounds, bounds, caption, minimized, maximized));
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void Negative_monitor_coordinates_preserve_the_pixel_tolerance(int inset, bool expected)
    {
        var monitor = new PixelRect(-2560, -300, 2560, 1440);
        var window = new PixelRect(monitor.X + inset, monitor.Y + inset, monitor.Width - inset * 2, monitor.Height - inset * 2);
        Assert.Equal(expected, MonitorCoverage.IsFullscreen(window, monitor, false, false, false));
    }

    private static readonly PixelRect Monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void Covers_WhenWindowMatchesMonitorExactly()
    {
        Assert.True(MonitorCoverage.Covers(new PixelRect(0, 0, 1920, 1080), Monitor));
    }

    [Fact]
    public void Covers_WhenWindowIsOnePixelLargerThanMonitor()
    {
        Assert.True(MonitorCoverage.Covers(new PixelRect(-1, -1, 1922, 1082), Monitor));
    }

    [Fact]
    public void DoesNotCover_WhenWindowEndsAtTaskbar()
    {
        Assert.False(MonitorCoverage.Covers(new PixelRect(0, 0, 1920, 1032), Monitor));
    }

    [Fact]
    public void DoesNotCover_WhenWindowOccupiesHalfTheMonitor()
    {
        Assert.False(MonitorCoverage.Covers(new PixelRect(8, 8, 952, 1064), Monitor));
    }

    [Fact]
    public void DoesNotCover_WhenRectangleIsEmpty()
    {
        Assert.False(MonitorCoverage.Covers(new PixelRect(0, 0, 0, 0), Monitor));
    }
}
