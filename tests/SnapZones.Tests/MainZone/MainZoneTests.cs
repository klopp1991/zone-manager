using SnapZones.Core.Editor;
using SnapZones.Core.Geometry;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using SnapZones.Core.Placement;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.MainZone;

/// <summary>
/// Die Hauptzone als Auffang für Fenster, die sonst niemandem zugeordnet werden können. Geprüft wird die
/// Auflösung, die Eindeutigkeit über die gesamte Konfiguration und der Entscheid, ob ein einzelnes
/// Fenster überhaupt aufgefangen wird.
/// </summary>
public sealed class MainZoneTests
{
    private static readonly Guid WorkLayoutId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EveningLayoutId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LeftZoneId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RightZoneId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid VideoZoneId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public void Without_a_marked_zone_there_is_no_main_zone()
    {
        Assert.Null(Core.Layouts.MainZone.Resolve(ConfigurationSamples.TwoLayouts()));
    }

    [Fact]
    public void The_marked_zone_of_the_active_layout_is_the_main_zone()
    {
        var configuration = WithMainZone(WorkLayoutId, RightZoneId);

        var resolved = Core.Layouts.MainZone.Resolve(configuration);

        Assert.NotNull(resolved);
        Assert.Equal(RightZoneId, resolved.Zone.Id);
        Assert.Equal("Arbeit / Rechts", resolved.DisplayName);
    }

    [Fact]
    public void A_main_zone_in_an_inactive_layout_does_not_apply()
    {
        var configuration = WithMainZone(EveningLayoutId, VideoZoneId);

        Assert.Null(Core.Layouts.MainZone.Resolve(configuration));
    }

    [Fact]
    public void A_deleted_zone_leaves_no_main_zone_behind()
    {
        var configuration = WithMainZone(WorkLayoutId, RightZoneId);
        configuration = configuration with
        {
            Layouts = configuration.Layouts
                .Select(layout => layout.Id == WorkLayoutId
                    ? layout with { Zones = layout.Zones.Where(zone => zone.Id != RightZoneId).ToArray() }
                    : layout)
                .ToArray()
        };

        Assert.Null(Core.Layouts.MainZone.Resolve(configuration));
    }

    [Fact]
    public void Setting_a_main_zone_removes_the_previous_one()
    {
        var service = new LayoutService(ConfigurationSamples.TwoLayouts());
        service.SetMainZone(EveningLayoutId, VideoZoneId);

        service.SetMainZone(WorkLayoutId, LeftZoneId);

        Assert.Equal(LeftZoneId, service.Configuration.Layouts.Single(layout => layout.Id == WorkLayoutId).MainZoneId);
        Assert.Null(service.Configuration.Layouts.Single(layout => layout.Id == EveningLayoutId).MainZoneId);
    }

    [Fact]
    public void A_main_zone_can_be_removed_again()
    {
        var service = new LayoutService(ConfigurationSamples.TwoLayouts());
        service.SetMainZone(WorkLayoutId, LeftZoneId);

        service.SetMainZone(WorkLayoutId, null);

        Assert.Null(service.ResolveMainZone());
    }

    [Fact]
    public void A_zone_of_another_layout_cannot_become_the_main_zone()
    {
        var service = new LayoutService(ConfigurationSamples.TwoLayouts());

        Assert.Throws<KeyNotFoundException>(() => service.SetMainZone(WorkLayoutId, VideoZoneId));
    }

    [Fact]
    public void A_copied_layout_does_not_inherit_the_main_zone()
    {
        var service = new LayoutService(ConfigurationSamples.TwoLayouts());
        service.SetMainZone(WorkLayoutId, LeftZoneId);

        var added = service.AddLayout(WorkLayoutId, "Fokus");

        Assert.Null(service.Configuration.Layouts.Single(layout => layout.Id == added.Id).MainZoneId);
        Assert.Equal(LeftZoneId, service.Configuration.Layouts.Single(layout => layout.Id == WorkLayoutId).MainZoneId);
    }

    [Fact]
    public void Saving_a_layout_with_a_main_zone_clears_the_marking_everywhere_else()
    {
        var service = new LayoutService(ConfigurationSamples.TwoLayouts());
        service.SetMainZone(EveningLayoutId, VideoZoneId);
        var work = service.Configuration.Layouts.Single(layout => layout.Id == WorkLayoutId);

        service.UpdateLayout(work with { MainZoneId = LeftZoneId });

        Assert.Null(service.Configuration.Layouts.Single(layout => layout.Id == EveningLayoutId).MainZoneId);
    }

    [Fact]
    public void Normalizing_keeps_only_the_first_valid_marking()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var layouts = configuration.Layouts
            .Select(layout => layout.Id == WorkLayoutId
                ? layout with { MainZoneId = LeftZoneId }
                : layout with { MainZoneId = VideoZoneId })
            .ToArray();

        var normalized = Core.Layouts.MainZone.Normalize(layouts);

        Assert.Equal(LeftZoneId, normalized.Single(layout => layout.Id == WorkLayoutId).MainZoneId);
        Assert.Null(normalized.Single(layout => layout.Id == EveningLayoutId).MainZoneId);
    }

    [Fact]
    public void A_window_outside_every_zone_is_caught_by_the_main_zone()
    {
        var configuration = WithMainZone(WorkLayoutId, RightZoneId);

        var bounds = MainZoneFallback.Resolve(configuration, Zones(), new PixelRect(1400, 600, 300, 200));

        Assert.Equal(new PixelRect(960, 0, 960, 1080), bounds);
    }

    [Fact]
    public void A_window_snapped_to_another_zone_is_left_alone()
    {
        var configuration = WithMainZone(WorkLayoutId, RightZoneId);

        Assert.Null(MainZoneFallback.Resolve(configuration, Zones(), new PixelRect(0, 0, 960, 1080)));
    }

    [Fact]
    public void The_invisible_window_border_still_counts_as_snapped()
    {
        var configuration = WithMainZone(WorkLayoutId, RightZoneId);

        // Ein eingerastetes Fenster meldet wegen des unsichtbaren Griffbereichs ein etwas groesseres
        // Rechteck als die Zone.
        Assert.Null(MainZoneFallback.Resolve(configuration, Zones(), new PixelRect(-7, 0, 974, 1087)));
    }

    [Fact]
    public void A_small_window_lying_over_a_zone_is_not_snapped()
    {
        Assert.False(MainZoneFallback.IsSnappedToAnyZone(new PixelRect(100, 100, 400, 300), Zones()));
    }

    [Fact]
    public void Without_a_main_zone_nothing_is_caught()
    {
        Assert.Null(MainZoneFallback.Resolve(
            ConfigurationSamples.TwoLayouts(),
            Zones(),
            new PixelRect(1400, 600, 300, 200)));
    }

    [Fact]
    public void The_editor_marks_and_unmarks_the_selected_zone()
    {
        var session = new LayoutEditorSession(Layout(WorkLayoutId));

        session.SetMainZone(RightZoneId);
        Assert.Equal(RightZoneId, session.MainZoneId);
        Assert.True(session.IsDirty);
        Assert.Equal(RightZoneId, session.CreateSnapshot().MainZoneId);

        session.SetMainZone(null);
        Assert.Null(session.MainZoneId);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void Deleting_the_main_zone_removes_the_marking()
    {
        var session = new LayoutEditorSession(Layout(WorkLayoutId));
        session.SetMainZone(RightZoneId);

        session.DeleteZone(RightZoneId);

        Assert.Null(session.MainZoneId);
    }

    [Fact]
    public void Applying_a_template_removes_the_marking()
    {
        var session = new LayoutEditorSession(Layout(WorkLayoutId));
        session.SetMainZone(RightZoneId);

        session.ReplaceZones(LayoutTemplates.Create(LayoutTemplate.ThreeColumns));

        Assert.Null(session.MainZoneId);
    }

    [Fact]
    public void Resetting_the_draft_restores_the_saved_marking()
    {
        var saved = Layout(WorkLayoutId) with { MainZoneId = LeftZoneId };
        var session = new LayoutEditorSession(saved);
        session.SetMainZone(RightZoneId);

        session.Reset();

        Assert.Equal(LeftZoneId, session.MainZoneId);
        Assert.False(session.IsDirty);
    }

    private static SnapConfiguration WithMainZone(Guid layoutId, Guid zoneId)
    {
        var service = new LayoutService(ConfigurationSamples.TwoLayouts());
        service.SetMainZone(layoutId, zoneId);
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
