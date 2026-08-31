using ZoneManager.App.ViewModels;
using ZoneManager.Core.Editor;
using ZoneManager.Core.Geometry;
using ZoneManager.Core.Models;
using ZoneManager.Core.Monitors;
using Xunit;

namespace ZoneManager.Tests.ViewModels;

public sealed class MainViewModelPersistenceTests
{
    private static readonly Guid FirstWorkId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FirstGameId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SecondWorkId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SecondMovieId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Valid_layout_change_persists_only_the_selected_layout()
    {
        var viewModel = CreateViewModel();
        SnapConfiguration? requested = null;
        viewModel.SaveRequested += configuration => requested = configuration;

        viewModel.Editor!.ApplyTemplate(LayoutTemplate.ThreeColumns);

        Assert.NotNull(requested);
        Assert.Equal(3, requested.Layouts.Single(layout => layout.Id == FirstWorkId).Zones.Count);
        Assert.Single(requested.Layouts.Single(layout => layout.Id == SecondWorkId).Zones);
    }

    [Fact]
    public void Setting_change_requests_persistence_immediately()
    {
        var viewModel = CreateViewModel();
        SnapConfiguration? requested = null;
        viewModel.SaveRequested += configuration => requested = configuration;

        viewModel.Settings.ZoneGap = 17;

        Assert.NotNull(requested);
        Assert.Equal(17, requested.Settings.ZoneGap);
        Assert.Equal(4, requested.Layouts.Count);
    }

    [Fact]
    public void Adding_a_layout_copies_and_activates_it_only_on_the_selected_monitor()
    {
        var viewModel = CreateViewModel();
        SnapConfiguration? requested = null;
        viewModel.SaveRequested += configuration => requested = configuration;

        viewModel.AddLayout();

        Assert.NotNull(requested);
        Assert.Equal(5, requested.Layouts.Count);
        Assert.Equal("Layout 1", viewModel.SelectedLayout!.Name);
        Assert.True(viewModel.SelectedLayout.IsActive);
        Assert.True(requested.Layouts.Single(layout => layout.Id == SecondWorkId).IsActive);
    }

    [Fact]
    public void Renaming_a_layout_requests_persistence_immediately()
    {
        var viewModel = CreateViewModel();
        SnapConfiguration? requested = null;
        viewModel.SaveRequested += configuration => requested = configuration;

        viewModel.RenameSelectedLayout("Büro");

        Assert.NotNull(requested);
        Assert.Equal("Büro", requested.Layouts.Single(layout => layout.Id == FirstWorkId).Name);
    }

    [Fact]
    public void Renaming_the_selected_monitor_persists_a_trimmed_alias_for_its_stable_identity()
    {
        var viewModel = CreateViewModel();
        SnapConfiguration? requested = null;
        viewModel.SaveRequested += configuration => requested = configuration;

        viewModel.RenameSelectedMonitor("  Links oben  ");

        Assert.NotNull(requested);
        Assert.Equal("Links oben", requested.MonitorNames["stable:DISPLAY-A"]);
        Assert.Equal("Links oben", viewModel.SelectedMonitor!.UserFacingName);
        Assert.Equal("Links oben", viewModel.GetMonitorDisplayName(viewModel.SelectedMonitor.Live.Identity));
        Assert.DoesNotContain("stable:DISPLAY-B", requested.MonitorNames.Keys);
    }

    [Fact]
    public void Empty_monitor_name_removes_the_alias_and_restores_the_automatic_name()
    {
        var configuration = Configuration() with
        {
            MonitorNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["stable:DISPLAY-A"] = "Links oben"
            }
        };
        var viewModel = new MainViewModel(configuration, LiveMonitors());

        viewModel.RenameSelectedMonitor("  ");

        Assert.Empty(viewModel.Configuration.MonitorNames);
        Assert.Equal("Monitor 1", viewModel.SelectedMonitor!.UserFacingName);
        Assert.Equal("Monitor 1", viewModel.GetMonitorDisplayName(viewModel.SelectedMonitor.Live.Identity));
    }

    [Fact]
    public void Moving_the_selected_monitor_up_persists_the_order_and_reorders_all_monitor_choices()
    {
        var viewModel = CreateViewModel();
        SnapConfiguration? requested = null;
        viewModel.SaveRequested += configuration => requested = configuration;
        viewModel.SelectedMonitor = viewModel.Monitors[1];

        viewModel.MoveSelectedMonitorUp();

        Assert.NotNull(requested);
        Assert.Equal(["stable:DISPLAY-B", "stable:DISPLAY-A"], requested.MonitorOrder);
        Assert.Equal(["DISPLAY-B", "DISPLAY-A"], viewModel.Monitors.Select(monitor => monitor.Live.Identity.StableId));
        Assert.Equal("DISPLAY-B", viewModel.SelectedMonitor!.Live.Identity.StableId);
    }

    [Fact]
    public void Deleting_the_active_layout_activates_another_layout_only_on_that_monitor()
    {
        var viewModel = CreateViewModel();
        SnapConfiguration? requested = null;
        viewModel.SaveRequested += configuration => requested = configuration;

        viewModel.DeleteSelectedLayout();

        Assert.NotNull(requested);
        Assert.Equal(3, requested.Layouts.Count);
        Assert.Equal(FirstGameId, viewModel.SelectedLayout!.Id);
        Assert.True(requested.Layouts.Single(layout => layout.Id == FirstGameId).IsActive);
        Assert.True(requested.Layouts.Single(layout => layout.Id == SecondWorkId).IsActive);
    }

    [Fact]
    public void Selecting_a_layout_activates_it_without_changing_the_other_monitor()
    {
        var viewModel = CreateViewModel();
        SnapConfiguration? requested = null;
        viewModel.SaveRequested += configuration => requested = configuration;

        viewModel.SelectedLayout = viewModel.Layouts.Single(layout => layout.Id == FirstGameId);

        Assert.NotNull(requested);
        Assert.True(requested.Layouts.Single(layout => layout.Id == FirstGameId).IsActive);
        Assert.False(requested.Layouts.Single(layout => layout.Id == FirstWorkId).IsActive);
        Assert.True(requested.Layouts.Single(layout => layout.Id == SecondWorkId).IsActive);
    }

    [Fact]
    public void Activating_a_layout_from_the_tray_changes_only_its_monitor()
    {
        var viewModel = CreateViewModel();
        SnapConfiguration? requested = null;
        viewModel.SaveRequested += configuration => requested = configuration;

        viewModel.ActivateLayout(SecondMovieId);

        Assert.NotNull(requested);
        Assert.True(requested.Layouts.Single(layout => layout.Id == FirstWorkId).IsActive);
        Assert.True(requested.Layouts.Single(layout => layout.Id == SecondMovieId).IsActive);
        Assert.False(requested.Layouts.Single(layout => layout.Id == SecondWorkId).IsActive);
    }

    [Fact]
    public void Replacing_configuration_applies_imported_layouts_and_settings()
    {
        var viewModel = CreateViewModel();
        var source = Configuration();
        var imported = source with
        {
            Settings = source.Settings with
            {
                ZoneGap = 37,
                ThemeMode = ThemeMode.Dark
            },
            Layouts = source.Layouts.Select(layout => layout.Id == FirstGameId
                ? layout with { IsActive = true }
                : layout.Id == FirstWorkId
                    ? layout with { IsActive = false }
                    : layout).ToArray()
        };

        viewModel.ReplaceConfiguration(imported);

        Assert.Equal(imported, viewModel.Configuration);
        Assert.Equal(37, viewModel.Settings.ZoneGap);
        Assert.Equal(ThemeMode.Dark, viewModel.Settings.ThemeMode);
        Assert.Equal(FirstGameId, viewModel.SelectedLayout!.Id);
        Assert.Equal(imported.Layouts.Single(layout => layout.Id == FirstGameId).Zones, viewModel.Editor!.Zones);
    }

    [Fact]
    public void Invalid_intermediate_setting_does_not_replace_the_persisted_snapshot()
    {
        var viewModel = CreateViewModel();
        var saveRequests = 0;
        viewModel.SaveRequested += _ => saveRequests++;

        viewModel.Settings.OverlayColor = "#12";

        Assert.Equal(0, saveRequests);
        Assert.Equal("Ungültige Eingabe", viewModel.StatusMessage);
    }

    private static MainViewModel CreateViewModel() => new(Configuration(), LiveMonitors());

    private static SnapConfiguration Configuration()
    {
        var first = new MonitorIdentity("DISPLAY-A", "\\\\.\\DISPLAY1", "Monitor A");
        var second = new MonitorIdentity("DISPLAY-B", "\\\\.\\DISPLAY2", "Monitor B");
        return new SnapConfiguration(
            SnapConfiguration.CurrentSchemaVersion,
            AppSettings.Default(Guid.Empty),
            [
                Layout(FirstWorkId, "Arbeit", first, true, "Voll"),
                Layout(FirstGameId, "Gaming", first, false, "Spiel"),
                Layout(SecondWorkId, "Arbeit", second, true, "Voll"),
                Layout(SecondMovieId, "Film", second, false, "Video")
            ]);
    }

    private static IReadOnlyList<LiveMonitor> LiveMonitors()
    {
        var first = new MonitorIdentity("DISPLAY-A", "\\\\.\\DISPLAY1", "Monitor A");
        var second = new MonitorIdentity("DISPLAY-B", "\\\\.\\DISPLAY2", "Monitor B");
        return
        [
            new LiveMonitor(first, new MonitorWorkArea(0, 0, 2560, 1440), 96, 96, true),
            new LiveMonitor(second, new MonitorWorkArea(2560, 0, 1920, 1080), 96, 96, false)
        ];
    }

    private static MonitorLayout Layout(
        Guid id,
        string name,
        MonitorIdentity monitor,
        bool isActive,
        string zoneName) =>
        new(monitor, 2560, 1440, [new ZoneDefinition(Guid.NewGuid(), zoneName, NormalizedRect.Full)])
        {
            Id = id,
            Name = name,
            IsActive = isActive
        };
}
