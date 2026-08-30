using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using Xunit;

namespace SnapZones.Tests.Geometry;

public sealed class LargestFreeRectangleTests
{
    [Fact]
    public void Find_returns_full_height_right_half_beside_left_zone()
    {
        var result = LargestFreeRectangle.Find([new NormalizedRect(0, 0, 0.5, 1)]);

        Assert.Equal(new NormalizedRect(0.5, 0, 0.5, 1), result);
    }

    [Fact]
    public void Find_prefers_top_then_left_when_free_areas_have_equal_size()
    {
        var result = LargestFreeRectangle.Find([new NormalizedRect(0, 0.4, 1, 0.2)]);

        Assert.Equal(new NormalizedRect(0, 0, 1, 0.4), result);
    }

    [Fact]
    public void Find_returns_null_when_monitor_is_fully_occupied()
    {
        var result = LargestFreeRectangle.Find([NormalizedRect.Full]);

        Assert.Null(result);
    }
}

