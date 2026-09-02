using SnapZones.Core.AppRules;
using SnapZones.Core.Geometry;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using SnapZones.Core.Placement;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.MainZone;

/// <summary>
/// Der Auffang beim Layoutwechsel. Er ist der eingreifendste Teil der Hauptzone — er bewegt bestehende
/// Fenster — und muss deshalb genau die Fenster treffen, die im neuen Layout wirklich heimatlos sind.
/// </summary>
public sealed class MainZoneSweepTests
{
    private static readonly Guid WorkLayoutId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LeftZoneId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RightZoneId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly PixelRect RightZoneBounds = new(960, 0, 960, 1080);
    private static readonly MonitorWorkArea WorkArea = new(0, 0, 1920, 1080);
    private static readonly AppWindowIdentity Editor = new(4711, @"C:\Programme\editor.exe", "Notizen", "Notepad");

    [Fact]
    public void A_stray_window_of_the_switched_monitor_moves_into_the_main_zone()
    {
        var planned = Plan(Configuration(), new MainZoneSweepWindow(17, new PixelRect(300, 200, 500, 400)));

        var target = Assert.Single(planned);
        Assert.Equal((nint)17, target.WindowHandle);
        Assert.Equal(RightZoneBounds, target.Bounds);
    }

    [Fact]
    public void A_window_snapped_to_a_zone_stays_where_it_is()
    {
        Assert.Empty(Plan(Configuration(), new MainZoneSweepWindow(17, new PixelRect(0, 0, 960, 1080))));
    }

    [Fact]
    public void A_window_on_another_monitor_is_not_touched()
    {
        // Der Wechsel betraf nur diesen einen Monitor; Fenster daneben gehen ihn nichts an.
        Assert.Empty(Plan(Configuration(), new MainZoneSweepWindow(17, new PixelRect(2400, 200, 500, 400))));
    }

    [Fact]
    public void Without_a_main_zone_nothing_is_collected()
    {
        var configuration = Configuration() with
        {
            Layouts = Configuration().Layouts.Select(layout => layout with { MainZoneId = null }).ToArray()
        };

        Assert.Empty(Plan(configuration, new MainZoneSweepWindow(17, new PixelRect(300, 200, 500, 400))));
    }

    [Fact]
    public void With_snapping_switched_off_nothing_is_collected()
    {
        var configuration = Configuration();
        configuration = configuration with
        {
            Settings = configuration.Settings with { SnappingEnabled = false }
        };

        Assert.Empty(Plan(configuration, new MainZoneSweepWindow(17, new PixelRect(300, 200, 500, 400))));
    }

    [Fact]
    public void An_excluded_window_is_left_alone()
    {
        var configuration = Configuration();
        configuration = configuration with
        {
            AppExclusions = [new AppExclusion(Guid.NewGuid(), "editor.exe", null, null, true)]
        };

        Assert.Empty(Plan(configuration, new MainZoneSweepWindow(17, new PixelRect(300, 200, 500, 400))));
    }

    [Fact]
    public void A_window_with_its_own_rule_is_left_to_the_rule()
    {
        var configuration = Configuration();
        configuration = configuration with
        {
            AppRules =
            [
                new AppRule(
                    Guid.NewGuid(),
                    "editor.exe",
                    null,
                    null,
                    AppRuleEvent.LayoutActivated,
                    0,
                    0,
                    0,
                    IsEnabled: true,
                    WorkLayoutId,
                    LeftZoneId)
            ]
        };

        Assert.Empty(Plan(configuration, new MainZoneSweepWindow(17, new PixelRect(300, 200, 500, 400))));
    }

    [Fact]
    public void A_disabled_rule_does_not_protect_the_window()
    {
        var configuration = Configuration();
        configuration = configuration with
        {
            AppRules =
            [
                new AppRule(
                    Guid.NewGuid(),
                    "editor.exe",
                    null,
                    null,
                    AppRuleEvent.LayoutActivated,
                    0,
                    0,
                    0,
                    IsEnabled: false,
                    WorkLayoutId,
                    LeftZoneId)
            ]
        };

        Assert.Single(Plan(configuration, new MainZoneSweepWindow(17, new PixelRect(300, 200, 500, 400))));
    }

    [Fact]
    public void A_window_without_a_readable_identity_is_left_alone()
    {
        var planned = MainZoneSweep.Plan(
            Configuration(),
            Zones(),
            WorkArea,
            [new MainZoneSweepWindow(17, new PixelRect(300, 200, 500, 400))],
            _ => null);

        Assert.Empty(planned);
    }

    [Fact]
    public void Several_stray_windows_are_all_collected()
    {
        var planned = Plan(
            Configuration(),
            new MainZoneSweepWindow(17, new PixelRect(300, 200, 500, 400)),
            new MainZoneSweepWindow(18, new PixelRect(100, 700, 400, 300)));

        Assert.Equal(2, planned.Count);
        Assert.All(planned, target => Assert.Equal(RightZoneBounds, target.Bounds));
    }

    private static IReadOnlyList<WindowPlacement> Plan(
        SnapConfiguration configuration,
        params MainZoneSweepWindow[] windows) =>
        MainZoneSweep.Plan(configuration, Zones(), WorkArea, windows, _ => Editor);

    private static SnapConfiguration Configuration()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        return configuration with
        {
            Settings = configuration.Settings with { SnappingEnabled = true },
            Layouts = configuration.Layouts
                .Select(layout => layout.Id == WorkLayoutId ? layout with { MainZoneId = RightZoneId } : layout)
                .ToArray()
        };
    }

    private static IReadOnlyList<PlacementZoneTarget> Zones() =>
    [
        new PlacementZoneTarget(WorkLayoutId, LeftZoneId, "DISPLAY-A", new PixelRect(0, 0, 960, 1080)),
        new PlacementZoneTarget(WorkLayoutId, RightZoneId, "DISPLAY-A", RightZoneBounds)
    ];
}
