using SnapZones.Core.Monitors;
using Xunit;

namespace SnapZones.Tests.Monitors;

public sealed class DisplayScalingTests
{
    [Theory]
    [InlineData(96u, 100)]
    [InlineData(120u, 125)]
    [InlineData(144u, 150)]
    [InlineData(168u, 175)]
    [InlineData(192u, 200)]
    [InlineData(150u, 150)]
    [InlineData(200u, 200)]
    [InlineData(480u, 500)]
    [InlineData(1000u, 500)]
    public void The_nearest_step_is_chosen_for_a_dpi_value(uint dpi, int expectedPercent)
    {
        Assert.Equal(expectedPercent, DisplayScaling.NearestPercent(dpi));
    }

    [Fact]
    public void Steps_and_dpi_values_round_trip()
    {
        Assert.Equal(2, DisplayScaling.IndexOf(150));
        Assert.Equal(-1, DisplayScaling.IndexOf(130));
        Assert.Equal(144u, DisplayScaling.DpiFor(150));
        Assert.Equal(96u, DisplayScaling.DpiFor(100));
        foreach (var percent in DisplayScaling.Percentages)
        {
            Assert.Equal(percent, DisplayScaling.NearestPercent(DisplayScaling.DpiFor(percent)));
        }
    }
}
