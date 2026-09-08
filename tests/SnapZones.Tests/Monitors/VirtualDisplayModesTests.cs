using SnapZones.Core.Monitors;
using Xunit;

namespace SnapZones.Tests.Monitors;

/// <summary>
/// Der Anzeigetreiber nimmt hoechstens hundert Modi an und liest sie aus <c>vdd_settings.xml</c>.
/// Die Liste muss die Zonengroessen exakt enthalten und darf die Grenze nie ueberschreiten.
/// </summary>
public sealed class VirtualDisplayModesTests
{
    [Fact]
    public void Zone_sizes_come_first_without_duplicates_and_common_sizes_fill_up()
    {
        var modes = VirtualDisplayModes.Build([new(2288, 1296), new(1382, 2488), new(2288, 1296)]);

        Assert.Equal(new VirtualDisplayMode(2288, 1296), modes[0]);
        Assert.Equal(new VirtualDisplayMode(1382, 2488), modes[1]);
        Assert.Equal(2 + VirtualDisplayModes.Common.Count, modes.Count);
        Assert.Equal(modes.Count, modes.Distinct().Count());
    }

    [Fact]
    public void Odd_sizes_are_rounded_down_to_even_and_clamped()
    {
        Assert.Equal(new VirtualDisplayMode(2296, 1296), VirtualDisplayModes.Normalize(2297, 1297));
        Assert.Equal(new VirtualDisplayMode(640, 480), VirtualDisplayModes.Normalize(100, 50));
        Assert.Equal(new VirtualDisplayMode(7680, 4320), VirtualDisplayModes.Normalize(9000, 9000));

        var modes = VirtualDisplayModes.Build([new(2297, 1297)]);
        Assert.Equal(new VirtualDisplayMode(2296, 1296), modes[0]);
    }

    [Fact]
    public void The_list_never_exceeds_the_driver_limit()
    {
        var many = Enumerable.Range(0, 150).Select(index => new VirtualDisplayMode(1000 + index * 2, 800));

        var modes = VirtualDisplayModes.Build(many);

        Assert.Equal(VirtualDisplayModes.MaximumCount, modes.Count);
        Assert.DoesNotContain(VirtualDisplayModes.Common[0], modes);
        Assert.Throws<ArgumentException>(() => VirtualDisplayModes.ToSettingsXml(modes.Concat([new(1, 1)]).ToArray()));
    }

    [Fact]
    public void Choosing_prefers_exact_then_the_largest_fitting_then_the_smallest_covering()
    {
        var available = new VirtualDisplayMode[] { new(1920, 1080), new(2288, 1296), new(2560, 1440), new(1280, 720) };

        Assert.Equal(new VirtualDisplayMode(2288, 1296), VirtualDisplayModes.Choose(available, 2289, 1297));
        Assert.Equal(new VirtualDisplayMode(1920, 1080), VirtualDisplayModes.Choose(available, 2000, 1200));
        Assert.Equal(new VirtualDisplayMode(1280, 720), VirtualDisplayModes.Choose(available, 1000, 600));
        Assert.Null(VirtualDisplayModes.Choose([new(3000, 100)], 640, 480));
        Assert.Null(VirtualDisplayModes.Choose([], 1920, 1080));
    }

    [Fact]
    public void The_settings_xml_lists_every_mode_at_sixty_hertz_and_no_global_rates()
    {
        var xml = VirtualDisplayModes.ToSettingsXml([new(2288, 1296), new(1920, 1080)]);

        Assert.Contains("<resolution><width>2288</width><height>1296</height><refresh_rate>60</refresh_rate></resolution>", xml);
        Assert.Contains("<resolution><width>1920</width><height>1080</height><refresh_rate>60</refresh_rate></resolution>", xml);
        Assert.Equal(2, xml.Split("<resolution>").Length - 1);
        Assert.DoesNotContain("g_refresh_rate", xml);
        Assert.Contains("<count>1</count>", xml);
        Assert.StartsWith("<?xml", xml);
    }
}
