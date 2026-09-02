using System.Text;
using SnapZones.Windows.Displays;
using Xunit;

namespace SnapZones.Tests.Monitors;

public sealed class EdidReaderTests
{
    [Fact]
    public void Decode_reads_manufacturer_product_code_and_serial_descriptor()
    {
        var edid = new byte[128];
        // "GSM" = G(7) S(19) M(13): 00111 10011 01101 -> 0x1E6D
        edid[8] = 0x1E;
        edid[9] = 0x6D;
        // Produktcode 0x9EB9 little-endian
        edid[10] = 0xB9;
        edid[11] = 0x9E;
        edid[21] = 88;
        edid[22] = 36;
        // Zweiter Beschreibungsblock: Seriennummer
        var offset = 54 + 18;
        edid[offset + 3] = 0xFF;
        var serial = Encoding.ASCII.GetBytes("404NTABC123\n ");
        Array.Copy(serial, 0, edid, offset + 5, serial.Length);

        var info = EdidReader.Decode(edid);

        Assert.NotNull(info);
        Assert.Equal("GSM9EB9", info.ProductCode);
        Assert.Equal("404NTABC123", info.SerialNumber);
        Assert.Equal(88, info.PhysicalSize?.PhysicalWidthCentimeters);
    }

    [Fact]
    public void Decode_reports_no_serial_when_the_descriptor_is_missing_or_all_zero()
    {
        var edid = new byte[128];
        edid[8] = 0x1E;
        edid[9] = 0x6D;
        Assert.Null(EdidReader.Decode(edid)!.SerialNumber);

        edid[54 + 3] = 0xFF;
        Array.Copy(Encoding.ASCII.GetBytes("0000000000000"), 0, edid, 54 + 5, 13);
        Assert.Null(EdidReader.Decode(edid)!.SerialNumber);
    }

    [Fact]
    public void Decode_handles_short_data()
    {
        Assert.Null(EdidReader.Decode(new byte[10]));
        Assert.Null(EdidReader.Decode(null));
    }
}
