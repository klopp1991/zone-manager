using SnapZones.Windows.Displays;
using Xunit;

namespace SnapZones.Tests.Monitors;

public sealed class EdidPhysicalSizeReaderTests
{
    [Fact]
    public void Decode_returns_physical_centimeters_from_valid_edid()
    {
        var edid = new byte[128];
        edid[21] = 119;
        edid[22] = 34;

        var size = EdidPhysicalSizeReader.Decode(edid);

        Assert.NotNull(size);
        Assert.Equal(119, size.PhysicalWidthCentimeters);
        Assert.Equal(34, size.PhysicalHeightCentimeters);
    }

    [Theory]
    [InlineData(0, 34)]
    [InlineData(119, 0)]
    public void Decode_ignores_edid_without_reliable_physical_dimensions(byte width, byte height)
    {
        var edid = new byte[128];
        edid[21] = width;
        edid[22] = height;

        Assert.Null(EdidPhysicalSizeReader.Decode(edid));
    }

    [Fact]
    public void Decode_ignores_short_edid_data()
    {
        Assert.Null(EdidPhysicalSizeReader.Decode(new byte[22]));
    }
}
