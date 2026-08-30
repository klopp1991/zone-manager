using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using Xunit;

namespace SnapZones.Tests.Geometry;

public sealed class ZoneEditorGeometryTests
{
    [Fact]
    public void ToValues_converts_normalized_bounds_to_literal_pixel_edges()
    {
        var bounds = new NormalizedRect(0.25, 0.1, 0.5, 0.6);

        var values = ZoneEditorGeometry.ToValues(bounds, MeasurementUnit.Pixels, 3440, 1400);

        Assert.Equal(new ZoneEditorValues(860, 140, 860, 420, 1720, 840), values);
    }

    [Fact]
    public void FromPositionAndSize_converts_percent_input_to_normalized_bounds()
    {
        var bounds = ZoneEditorGeometry.FromPositionAndSize(
            10, 5, 70, 80, MeasurementUnit.Percent, 2560, 1440);

        Assert.Equal(new NormalizedRect(0.1, 0.05, 0.7, 0.8), bounds);
    }

    [Fact]
    public void FromMargins_converts_pixel_edges_to_normalized_bounds()
    {
        var bounds = ZoneEditorGeometry.FromMargins(
            200, 100, 300, 200, MeasurementUnit.Pixels, 2000, 1000);

        Assert.Equal(new NormalizedRect(0.1, 0.1, 0.75, 0.7), bounds);
    }

    [Fact]
    public void FromPositionAndSize_converts_each_value_in_its_own_unit()
    {
        var bounds = ZoneEditorGeometry.FromPositionAndSize(
            new ZoneMeasurement(320, MeasurementUnit.Pixels),
            new ZoneMeasurement(10, MeasurementUnit.Percent),
            new ZoneMeasurement(50, MeasurementUnit.Percent),
            new ZoneMeasurement(540, MeasurementUnit.Pixels),
            3200,
            1080);

        Assert.Equal(new NormalizedRect(0.1, 0.1, 0.5, 0.5), bounds);
    }

    [Fact]
    public void FromMargins_converts_each_value_in_its_own_unit()
    {
        var bounds = ZoneEditorGeometry.FromMargins(
            new ZoneMeasurement(320, MeasurementUnit.Pixels),
            new ZoneMeasurement(10, MeasurementUnit.Percent),
            new ZoneMeasurement(640, MeasurementUnit.Pixels),
            new ZoneMeasurement(20, MeasurementUnit.Percent),
            3200,
            1080);

        Assert.Equal(new NormalizedRect(0.1, 0.1, 0.7, 0.7), bounds);
    }
}
