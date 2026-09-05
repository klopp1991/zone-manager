using SnapZones.Core.AppRules;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Placement;
using SnapZones.Tests.Support;
using SnapZones.Windows.Windows;
using Xunit;

namespace SnapZones.Tests.Placement;

/// <summary>
/// Das Platzierungs-Modul am gestellten Fenstersatz. Geprüft wird die vollständige Zuordnungskette beim
/// Erscheinen eines Fensters — Regel, gemerkte Position, Hauptzone —, dazu Ausschlüsse und der
/// abschaltbare Positionskatalog.
/// </summary>
public sealed class WindowPlacementEngineTests
{
    private static readonly Guid LayoutId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LeftZoneId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RightZoneId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly PixelRect LeftZoneBounds = new(0, 0, 960, 1080);
    private static readonly PixelRect RightZoneBounds = new(960, 0, 960, 1080);
    private static readonly PixelRect Stray = new(300, 200, 500, 400);
    private static readonly WindowIdentity EditorIdentity = new(@"C:\Programme\editor.exe", "Notepad", WindowKind.MainWindow);

    [Fact]
    public async Task A_new_window_without_any_assignment_lands_in_the_main_zone()
    {
        using var harness = Harness(mainZoneId: RightZoneId);
        harness.WindowService.Add(Window(17, Stray));
        harness.Engine.Start();

        await harness.ShowWindowAsync(17);

        var placement = Assert.Single(harness.WindowService.Placements);
        Assert.Equal(RightZoneBounds, placement.Bounds);
        Assert.False(placement.Maximize);
    }

    [Fact]
    public async Task A_new_window_already_snapped_to_a_zone_is_not_moved()
    {
        using var harness = Harness(mainZoneId: RightZoneId);
        harness.WindowService.Add(Window(17, LeftZoneBounds));
        harness.Engine.Start();

        await harness.ShowWindowAsync(17);

        Assert.Empty(harness.WindowService.Placements);
    }

    [Fact]
    public async Task A_maximized_window_keeps_its_size()
    {
        using var harness = Harness(mainZoneId: RightZoneId);
        harness.WindowService.Add(Window(17, Stray) with { IsMaximized = true });
        harness.Engine.Start();

        await harness.ShowWindowAsync(17);

        Assert.Empty(harness.WindowService.Placements);
    }

    [Fact]
    public async Task Without_a_main_zone_a_new_window_is_left_where_windows_put_it()
    {
        using var harness = Harness(mainZoneId: null);
        harness.WindowService.Add(Window(17, Stray));
        harness.Engine.Start();

        await harness.ShowWindowAsync(17);

        Assert.Empty(harness.WindowService.Placements);
    }

    [Fact]
    public async Task A_remembered_position_wins_over_the_main_zone()
    {
        var remembered = new PixelRect(120, 80, 640, 480);
        using var harness = Harness(mainZoneId: RightZoneId, catalog: Catalog(remembered));
        harness.WindowService.Add(Window(17, Stray));
        harness.Engine.Start();

        await harness.ShowWindowAsync(17);

        Assert.Equal(remembered, Assert.Single(harness.WindowService.Placements).Bounds);
    }

    [Fact]
    public async Task A_configured_rule_wins_over_the_main_zone()
    {
        using var harness = Harness(mainZoneId: RightZoneId);
        harness.Configuration = harness.Configuration with
        {
            AppRules =
            [
                new AppRule(
                    Guid.NewGuid(),
                    "editor.exe",
                    null,
                    null,
                    AppRuleEvent.WindowCreated,
                    0,
                    0,
                    0,
                    IsEnabled: true,
                    LayoutId,
                    LeftZoneId)
            ]
        };
        harness.WindowService.Add(Window(17, Stray));
        harness.Engine.Start();

        await harness.ShowWindowAsync(17);

        // Die Regel selbst wird vom Regel-Koordinator ausgefuehrt; das Platzierungs-Modul haelt
        // sich hier vollstaendig heraus, statt das Fenster vorher in die Hauptzone zu ziehen.
        Assert.Empty(harness.WindowService.Placements);
    }

    [Fact]
    public async Task An_excluded_window_is_neither_placed_nor_remembered()
    {
        using var harness = Harness(mainZoneId: RightZoneId);
        harness.Configuration = harness.Configuration with
        {
            AppExclusions = [new AppExclusion(Guid.NewGuid(), "editor.exe", null, null, true)]
        };
        harness.WindowService.Add(Window(17, Stray));
        harness.Engine.Start();

        await harness.ShowWindowAsync(17);
        await harness.EndMoveAsync(17);

        Assert.Empty(harness.WindowService.Placements);
        Assert.Empty(harness.Engine.Catalog.Entries);
    }

    [Fact]
    public async Task A_disabled_exclusion_leaves_the_main_zone_in_charge()
    {
        using var harness = Harness(mainZoneId: RightZoneId);
        harness.Configuration = harness.Configuration with
        {
            AppExclusions = [new AppExclusion(Guid.NewGuid(), "editor.exe", null, null, false)]
        };
        harness.WindowService.Add(Window(17, Stray));
        harness.Engine.Start();

        await harness.ShowWindowAsync(17);

        Assert.Equal(RightZoneBounds, Assert.Single(harness.WindowService.Placements).Bounds);
    }

    [Fact]
    public async Task Without_an_active_layout_nothing_happens_at_all()
    {
        using var harness = Harness(mainZoneId: RightZoneId);
        harness.Configuration = harness.Configuration with
        {
            Layouts = harness.Configuration.Layouts.Select(layout => layout with { IsActive = false }).ToArray()
        };
        harness.WindowService.Add(Window(17, Stray));
        harness.Engine.Start();

        await harness.ShowWindowAsync(17);
        await harness.EndMoveAsync(17);

        Assert.Empty(harness.WindowService.Placements);
        Assert.Empty(harness.Engine.Catalog.Entries);
    }

    [Fact]
    public async Task A_focus_change_never_moves_a_window()
    {
        using var harness = Harness(mainZoneId: RightZoneId);
        harness.WindowService.Add(Window(17, Stray));
        harness.Engine.Start();

        await harness.FocusWindowAsync(17);

        Assert.Empty(harness.WindowService.Placements);
    }

    [Fact]
    public async Task A_placed_window_is_taken_into_the_catalogue()
    {
        using var harness = Harness(mainZoneId: RightZoneId);
        harness.WindowService.Add(Window(17, LeftZoneBounds));
        harness.Engine.Start();

        await harness.EndMoveAsync(17);

        var entry = Assert.Single(harness.Engine.Catalog.Entries);
        Assert.Equal(EditorIdentity, entry.Identity);
        Assert.Equal(LeftZoneBounds, entry.NormalBoundsPixels);
        Assert.Equal(LeftZoneId, entry.ZoneId);
    }

    [Fact]
    public async Task A_window_in_fullscreen_keeps_its_remembered_zone_instead_of_the_monitor_rectangle()
    {
        // Ein Browser im Vollbild nimmt den ganzen Monitor ein. Dieses Rechteck darf den Katalog nicht
        // ueberschreiben, sonst erscheint das Fenster beim naechsten Start monitorfuellend statt in
        // seiner Zone.
        using var harness = Harness(mainZoneId: RightZoneId);
        harness.WindowService.Add(Window(17, LeftZoneBounds));
        harness.Engine.Start();
        await harness.EndMoveAsync(17);

        harness.WindowService.Add(Window(17, new PixelRect(0, 0, 1920, 1080)) with { IsFullscreen = true });
        await harness.EndMoveAsync(17);

        var entry = Assert.Single(harness.Engine.Catalog.Entries);
        Assert.Equal(LeftZoneBounds, entry.NormalBoundsPixels);
        Assert.Equal(LeftZoneId, entry.ZoneId);
    }

    [Fact]
    public async Task With_remembering_switched_off_the_main_zone_still_catches_new_windows()
    {
        // Die Hauptzone haengt nicht am Positionskatalog: wer das Merken abschaltet, verliert sie nicht.
        using var harness = Harness(mainZoneId: RightZoneId);
        harness.Configuration = harness.Configuration with
        {
            Settings = harness.Configuration.Settings with { RememberWindowPositions = false }
        };
        harness.WindowService.Add(Window(17, Stray));
        harness.Engine.Start();

        await harness.ShowWindowAsync(17);
        await harness.EndMoveAsync(17);

        Assert.Equal(RightZoneBounds, Assert.Single(harness.WindowService.Placements).Bounds);
        Assert.Empty(harness.Engine.Catalog.Entries);
    }

    [Fact]
    public async Task With_remembering_switched_off_a_stored_entry_is_not_applied()
    {
        var remembered = new PixelRect(120, 80, 640, 480);
        using var harness = Harness(mainZoneId: null, catalog: Catalog(remembered));
        harness.Configuration = harness.Configuration with
        {
            Settings = harness.Configuration.Settings with { RememberWindowPositions = false }
        };
        harness.WindowService.Add(Window(17, Stray));
        harness.Engine.Start();

        await harness.ShowWindowAsync(17);

        Assert.Empty(harness.WindowService.Placements);
        // Bestehende Eintraege bleiben erhalten, sie werden nur nicht mehr angewendet.
        Assert.Single(harness.Engine.Catalog.Entries);
    }

    [Fact]
    public async Task Forgetting_everything_empties_the_catalogue_and_writes_it_out()
    {
        using var harness = Harness(mainZoneId: null, catalog: Catalog(new PixelRect(120, 80, 640, 480)));
        harness.Engine.Start();

        harness.Engine.ForgetAll();
        await harness.Engine.FlushAsync(CancellationToken.None);

        Assert.Empty(harness.Engine.Catalog.Entries);
        Assert.NotNull(harness.Repository.Latest);
        Assert.Empty(harness.Repository.Latest.Entries);
    }

    private static PlacementEngineHarness Harness(Guid? mainZoneId, WindowPlacementCatalog? catalog = null)
    {
        var zones = new ZoneDefinition[]
        {
            new(LeftZoneId, "Links", new NormalizedRect(0, 0, 0.5, 1)),
            new(RightZoneId, "Rechts", new NormalizedRect(0.5, 0, 0.5, 1))
        };
        var monitor = new MonitorIdentity("DISPLAY-A", @"\\.\DISPLAY1", "Hauptmonitor");
        var configuration = new SnapConfiguration(
            SnapConfiguration.CurrentSchemaVersion,
            AppSettings.Default(Guid.Empty),
            [
                new MonitorLayout(monitor, 1920, 1080, zones)
                {
                    Id = LayoutId,
                    Name = "Arbeit",
                    IsActive = true,
                    MainZoneId = mainZoneId
                }
            ]);

        return new PlacementEngineHarness(
            configuration,
            [
                new PlacementZoneTarget(LayoutId, LeftZoneId, "DISPLAY-A", LeftZoneBounds),
                new PlacementZoneTarget(LayoutId, RightZoneId, "DISPLAY-A", RightZoneBounds)
            ],
            catalog);
    }

    private static WindowPlacementCatalog Catalog(PixelRect bounds) => new(
        WindowPlacementCatalog.CurrentSchemaVersion,
        [
            new WindowPlacementEntry(
                EditorIdentity,
                "DISPLAY-A",
                null,
                new MonitorWorkArea(0, 0, 1920, 1080),
                bounds,
                new NormalizedRect(0, 0, 0.5, 0.5),
                WasMaximized: false,
                DateTimeOffset.UnixEpoch)
        ]);

    [Fact]
    public async Task A_window_that_may_not_be_placed_automatically_is_left_alone()
    {
        // Ein Kontextmenue oder ein Dialog erscheint wie jedes andere Fenster; die Begruendung aus
        // AutomaticPlacement haengt an der Aufnahme und haelt den Auffang zurueck.
        using var harness = Harness(mainZoneId: RightZoneId);
        harness.WindowService.Add(Window(17, Stray) with
        {
            AutomaticPlacementRejection = AutomaticPlacementRejection.TransientClass
        });
        harness.Engine.Start();

        await harness.ShowWindowAsync(17);

        Assert.Empty(harness.WindowService.Placements);
    }

    [Fact]
    public async Task A_dialog_is_not_restored_to_its_remembered_position_either()
    {
        var remembered = new PixelRect(120, 80, 640, 480);
        using var harness = Harness(mainZoneId: RightZoneId, catalog: Catalog(remembered));
        harness.WindowService.Add(Window(17, Stray) with
        {
            AutomaticPlacementRejection = AutomaticPlacementRejection.NoMaximizeBox
        });
        harness.Engine.Start();

        await harness.ShowWindowAsync(17);

        Assert.Empty(harness.WindowService.Placements);
    }

    private static PlacementWindowSnapshot Window(nint handle, PixelRect bounds) => new(
        handle,
        EditorIdentity,
        "Notizen",
        bounds,
        bounds,
        IsMaximized: false,
        IsMinimized: false,
        ProcessPath: @"C:\Programme\editor.exe");
}
