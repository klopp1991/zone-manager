using SnapZones.Core.Editor;
using SnapZones.Core.Geometry;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.Placement;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.StartZone;

/// <summary>
/// Die Startzone als Auffang für Fenster, die sonst niemandem zugeordnet werden können. Geprüft wird die
/// Auflösung, die Eindeutigkeit über die gesamte Konfiguration und der Entscheid, ob ein einzelnes
/// Fenster überhaupt aufgefangen wird.
/// </summary>
public sealed class StartZoneTests
{
    private static readonly Guid WorkLayoutId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EveningLayoutId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LeftZoneId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RightZoneId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid VideoZoneId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public void Without_a_marked_zone_there_is_no_main_zone()
    {
        Assert.Null(Core.Layouts.StartZone.Resolve(ConfigurationSamples.TwoLayouts()));
    }

    [Fact]
    public void The_marked_zone_of_the_active_layout_is_the_main_zone()
    {
        var configuration = WithStartZone(WorkLayoutId, RightZoneId);

        var resolved = Core.Layouts.StartZone.Resolve(configuration);

        Assert.NotNull(resolved);
        Assert.Equal(RightZoneId, resolved.Zone.Id);
        Assert.Equal("Arbeit / Rechts", resolved.DisplayName);
    }

    [Fact]
    public void A_main_zone_in_an_inactive_layout_does_not_apply()
    {
        var configuration = WithStartZone(EveningLayoutId, VideoZoneId);

        Assert.Null(Core.Layouts.StartZone.Resolve(configuration));
    }

    [Fact]
    public void A_deleted_zone_leaves_no_main_zone_behind()
    {
        var configuration = WithStartZone(WorkLayoutId, RightZoneId);
        configuration = configuration with
        {
            Layouts = configuration.Layouts
                .Select(layout => layout.Id == WorkLayoutId
                    ? layout with { Zones = layout.Zones.Where(zone => zone.Id != RightZoneId).ToArray() }
                    : layout)
                .ToArray()
        };

        Assert.Null(Core.Layouts.StartZone.Resolve(configuration));
    }

    [Fact]
    public void Every_layout_may_carry_its_own_main_zone()
    {
        var service = new LayoutService(ConfigurationSamples.TwoLayouts());
        service.SetStartZone(EveningLayoutId, VideoZoneId);

        service.SetStartZone(WorkLayoutId, LeftZoneId);

        Assert.Equal(LeftZoneId, service.Configuration.Layouts.Single(layout => layout.Id == WorkLayoutId).StartZoneId);
        Assert.Equal(VideoZoneId, service.Configuration.Layouts.Single(layout => layout.Id == EveningLayoutId).StartZoneId);
    }

    [Fact]
    public void The_marking_survives_a_layout_switch_when_both_layouts_carry_one()
    {
        var service = new LayoutService(ConfigurationSamples.TwoLayouts());
        service.SetStartZone(WorkLayoutId, LeftZoneId);
        service.SetStartZone(EveningLayoutId, VideoZoneId);

        service.ActivateLayout(EveningLayoutId);

        Assert.Equal(VideoZoneId, service.ResolveStartZone()?.Zone.Id);
    }

    [Fact]
    public void The_monitor_order_decides_which_marking_applies()
    {
        var configuration = TwoMonitors();

        // Ohne festgelegte Reihenfolge gewinnt das zuerst gespeicherte Layout.
        Assert.Equal("Links", Core.Layouts.StartZone.Resolve(configuration)?.Zone.Name);

        var service = new LayoutService(configuration);
        service.UpdateMonitorOrder([SecondMonitor, FirstMonitor]);

        Assert.Equal("Video", service.ResolveStartZone()?.Zone.Name);
    }

    [Fact]
    public void A_main_zone_can_be_removed_again()
    {
        var service = new LayoutService(ConfigurationSamples.TwoLayouts());
        service.SetStartZone(WorkLayoutId, LeftZoneId);

        service.SetStartZone(WorkLayoutId, null);

        Assert.Null(service.ResolveStartZone());
    }

    [Fact]
    public void A_zone_of_another_layout_cannot_become_the_main_zone()
    {
        var service = new LayoutService(ConfigurationSamples.TwoLayouts());

        Assert.Throws<KeyNotFoundException>(() => service.SetStartZone(WorkLayoutId, VideoZoneId));
    }

    [Fact]
    public void A_copied_layout_inherits_the_marking_on_its_own_new_zone()
    {
        var service = new LayoutService(ConfigurationSamples.TwoLayouts());
        service.SetStartZone(WorkLayoutId, LeftZoneId);

        var added = service.AddLayout(WorkLayoutId, "Fokus");

        var copy = service.Configuration.Layouts.Single(layout => layout.Id == added.Id);
        Assert.Equal(copy.Zones[0].Id, copy.StartZoneId);
        Assert.NotEqual(LeftZoneId, copy.StartZoneId);
        Assert.Equal(LeftZoneId, service.Configuration.Layouts.Single(layout => layout.Id == WorkLayoutId).StartZoneId);
    }

    [Fact]
    public void A_copy_of_a_layout_without_a_marking_has_none_either()
    {
        var service = new LayoutService(ConfigurationSamples.TwoLayouts());

        var added = service.AddLayout(WorkLayoutId, "Fokus");

        Assert.Null(service.Configuration.Layouts.Single(layout => layout.Id == added.Id).StartZoneId);
    }

    [Fact]
    public void Normalizing_drops_only_markings_pointing_at_a_missing_zone()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var layouts = configuration.Layouts
            .Select(layout => layout.Id == WorkLayoutId
                ? layout with { StartZoneId = LeftZoneId }
                : layout with { StartZoneId = LeftZoneId })
            .ToArray();

        var normalized = Core.Layouts.StartZone.Normalize(layouts);

        Assert.Equal(LeftZoneId, normalized.Single(layout => layout.Id == WorkLayoutId).StartZoneId);
        Assert.Null(normalized.Single(layout => layout.Id == EveningLayoutId).StartZoneId);
    }

    [Fact]
    public void A_window_outside_every_zone_is_caught_by_the_main_zone()
    {
        var configuration = WithStartZone(WorkLayoutId, RightZoneId);

        var bounds = StartZoneFallback.Resolve(configuration, Zones(), new PixelRect(1400, 600, 300, 200));

        Assert.Equal(new PixelRect(960, 0, 960, 1080), bounds);
    }

    [Fact]
    public void A_window_snapped_to_another_zone_is_left_alone()
    {
        var configuration = WithStartZone(WorkLayoutId, RightZoneId);

        Assert.Null(StartZoneFallback.Resolve(configuration, Zones(), new PixelRect(0, 0, 960, 1080)));
    }

    [Fact]
    public void The_invisible_window_border_still_counts_as_snapped()
    {
        var configuration = WithStartZone(WorkLayoutId, RightZoneId);

        // Ein eingerastetes Fenster meldet wegen des unsichtbaren Griffbereichs ein etwas groesseres
        // Rechteck als die Zone.
        Assert.Null(StartZoneFallback.Resolve(configuration, Zones(), new PixelRect(-7, 0, 974, 1087)));
    }

    [Fact]
    public void A_small_window_lying_over_a_zone_is_not_snapped()
    {
        Assert.False(StartZoneFallback.IsSnappedToAnyZone(new PixelRect(100, 100, 400, 300), Zones()));
    }

    [Fact]
    public void Without_a_main_zone_nothing_is_caught()
    {
        Assert.Null(StartZoneFallback.Resolve(
            ConfigurationSamples.TwoLayouts(),
            Zones(),
            new PixelRect(1400, 600, 300, 200)));
    }

    [Fact]
    public void The_editor_marks_and_unmarks_the_selected_zone()
    {
        var session = new LayoutEditorSession(Layout(WorkLayoutId));

        session.SetStartZone(RightZoneId);
        Assert.Equal(RightZoneId, session.StartZoneId);
        Assert.True(session.IsDirty);
        Assert.Equal(RightZoneId, session.CreateSnapshot().StartZoneId);

        session.SetStartZone(null);
        Assert.Null(session.StartZoneId);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void Deleting_the_main_zone_removes_the_marking()
    {
        var session = new LayoutEditorSession(Layout(WorkLayoutId));
        session.SetStartZone(RightZoneId);

        session.DeleteZone(RightZoneId);

        Assert.Null(session.StartZoneId);
    }

    [Fact]
    public void Applying_a_template_removes_the_marking()
    {
        var session = new LayoutEditorSession(Layout(WorkLayoutId));
        session.SetStartZone(RightZoneId);

        session.ReplaceZones(LayoutTemplates.Create(LayoutTemplate.ThreeColumns));

        Assert.Null(session.StartZoneId);
    }

    [Fact]
    public void Resetting_the_draft_restores_the_saved_marking()
    {
        var saved = Layout(WorkLayoutId) with { StartZoneId = LeftZoneId };
        var session = new LayoutEditorSession(saved);
        session.SetStartZone(RightZoneId);

        session.Reset();

        Assert.Equal(LeftZoneId, session.StartZoneId);
        Assert.False(session.IsDirty);
    }

    private static readonly MonitorIdentity FirstMonitor =
        new("DISPLAY-A", @"\\.\DISPLAY1", "Hauptmonitor");

    private static readonly MonitorIdentity SecondMonitor =
        new("DISPLAY-B", @"\\.\DISPLAY2", "Zweitmonitor");

    /// <summary>Zwei Monitore, deren aktives Layout je eine Startzone traegt.</summary>
    private static SnapConfiguration TwoMonitors() => new(
        SnapConfiguration.CurrentSchemaVersion,
        AppSettings.Default(Guid.Empty),
        [
            new MonitorLayout(FirstMonitor, 1920, 1080, [new ZoneDefinition(LeftZoneId, "Links", NormalizedRect.Full)])
            {
                Id = WorkLayoutId,
                Name = "Arbeit",
                IsActive = true,
                StartZoneId = LeftZoneId
            },
            new MonitorLayout(SecondMonitor, 1920, 1080, [new ZoneDefinition(VideoZoneId, "Video", NormalizedRect.Full)])
            {
                Id = EveningLayoutId,
                Name = "Abend",
                IsActive = true,
                StartZoneId = VideoZoneId
            }
        ]);

    private static SnapConfiguration WithStartZone(Guid layoutId, Guid zoneId)
    {
        var service = new LayoutService(ConfigurationSamples.TwoLayouts());
        service.SetStartZone(layoutId, zoneId);
        return service.Configuration;
    }

    private static MonitorLayout Layout(Guid layoutId) =>
        ConfigurationSamples.TwoLayouts().Layouts.Single(layout => layout.Id == layoutId);

    /// <summary>Die Zonen des aktiven Layouts auf einem 1920x1080-Monitor.</summary>
    private static IReadOnlyList<PlacementZoneTarget> Zones() =>
    [
        new PlacementZoneTarget(WorkLayoutId, LeftZoneId, "DISPLAY-A", new PixelRect(0, 0, 960, 1080)),
        new PlacementZoneTarget(WorkLayoutId, RightZoneId, "DISPLAY-A", new PixelRect(960, 0, 960, 1080))
    ];
}
