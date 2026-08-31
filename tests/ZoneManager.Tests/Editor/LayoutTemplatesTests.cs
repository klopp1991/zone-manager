using ZoneManager.Core.Editor;
using ZoneManager.Core.Geometry;
using Xunit;

namespace ZoneManager.Tests.Editor;

public sealed class LayoutTemplatesTests
{
    [Theory]
    [InlineData(LayoutTemplate.TwoColumns, 2)]
    [InlineData(LayoutTemplate.ThreeColumns, 3)]
    [InlineData(LayoutTemplate.MainAndSide, 2)]
    [InlineData(LayoutTemplate.Grid2x2, 4)]
    public void Create_returns_non_overlapping_full_bounds(LayoutTemplate template, int expectedCount)
    {
        var zones = LayoutTemplates.Create(template);

        Assert.Equal(expectedCount, zones.Count);
        Assert.True(ZoneGeometry.Validate(zones).IsValid);
        Assert.Equal(1.0, zones.Sum(zone => zone.Bounds.Width * zone.Bounds.Height), 6);
    }
}
