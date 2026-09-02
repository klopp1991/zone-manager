using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using Xunit;

namespace SnapZones.Tests.Layouts;

public sealed class LayoutServiceTests
{
    private static readonly Guid FirstMonitorWorkId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FirstMonitorGameId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SecondMonitorWorkId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SecondMonitorMovieId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Activating_a_layout_changes_only_its_monitor()
    {
        var service = new LayoutService(Configuration());

        service.ActivateLayout(FirstMonitorGameId);

        Assert.False(service.Configuration.Layouts.Single(layout => layout.Id == FirstMonitorWorkId).IsActive);
        Assert.True(service.Configuration.Layouts.Single(layout => layout.Id == FirstMonitorGameId).IsActive);
        Assert.True(service.Configuration.Layouts.Single(layout => layout.Id == SecondMonitorWorkId).IsActive);
        Assert.False(service.Configuration.Layouts.Single(layout => layout.Id == SecondMonitorMovieId).IsActive);
    }

    [Fact]
    public void Adding_a_layout_copies_only_the_source_monitor_and_activates_the_copy()
    {
        var service = new LayoutService(Configuration());

        var added = service.AddLayout(FirstMonitorWorkId, "Fokus");

        Assert.Equal("DISPLAY-A", added.Monitor.StableId);
        Assert.True(added.IsActive);
        Assert.All(added.Zones, zone => Assert.DoesNotContain(
            service.Configuration.Layouts.Single(layout => layout.Id == FirstMonitorWorkId).Zones,
            source => source.Id == zone.Id));
        Assert.True(service.Configuration.Layouts.Single(layout => layout.Id == SecondMonitorWorkId).IsActive);
    }

    [Fact]
    public void Deleting_the_active_layout_activates_another_layout_on_the_same_monitor()
    {
        var service = new LayoutService(Configuration());

        service.DeleteLayout(FirstMonitorWorkId);

        Assert.True(service.Configuration.Layouts.Single(layout => layout.Id == FirstMonitorGameId).IsActive);
        Assert.True(service.Configuration.Layouts.Single(layout => layout.Id == SecondMonitorWorkId).IsActive);
    }

    [Fact]
    public void Deleting_the_last_layout_of_a_monitor_is_rejected()
    {
        var configuration = Configuration() with
        {
            Layouts = Configuration().Layouts.Where(layout => layout.Monitor.StableId == "DISPLAY-A").Take(1).ToArray()
        };
        var service = new LayoutService(configuration);

        Assert.Throws<InvalidOperationException>(() => service.DeleteLayout(FirstMonitorWorkId));
    }

    [Fact]
    public void A_recycled_device_name_does_not_mix_layouts_of_different_monitors()
    {
        var service = new LayoutService(Configuration());

        // Windows hat den GDI-Namen \\.\DISPLAY1 inzwischen an Monitor B vergeben.
        var recycled = new MonitorIdentity("DISPLAY-B", "\\\\.\\DISPLAY1", "Monitor B");

        var layouts = service.LayoutsFor(recycled);

        Assert.Equal(2, layouts.Count);
        Assert.All(layouts, layout => Assert.Equal("DISPLAY-B", layout.Monitor.StableId));
        Assert.Equal(SecondMonitorWorkId, service.EnsureMonitor(recycled, 2560, 1440).Id);
    }

    [Fact]
    public void Layouts_without_stable_id_still_match_by_device_name()
    {
        var service = new LayoutService(Configuration());

        var legacy = new MonitorIdentity(string.Empty, "\\\\.\\DISPLAY1", "Monitor A");

        Assert.Equal(2, service.LayoutsFor(legacy).Count);
    }

    [Fact]
    public void Several_active_layouts_on_one_monitor_are_repaired_instead_of_throwing()
    {
        var configuration = Configuration();
        var broken = configuration with
        {
            Layouts = configuration.Layouts
                .Select(layout => layout.Id == FirstMonitorGameId ? layout with { IsActive = true } : layout)
                .ToArray()
        };
        var service = new LayoutService(broken);
        var monitor = new MonitorIdentity("DISPLAY-A", "\\\\.\\DISPLAY1", "Monitor A");

        var active = service.ActiveLayoutFor(monitor);

        Assert.Equal(FirstMonitorWorkId, active.Id);
        Assert.Single(service.LayoutsFor(monitor), layout => layout.IsActive);
    }

    [Fact]
    public void A_monitor_without_any_active_layout_gets_one_activated()
    {
        var configuration = Configuration();
        var broken = configuration with
        {
            Layouts = configuration.Layouts
                .Select(layout => layout.Id == FirstMonitorWorkId ? layout with { IsActive = false } : layout)
                .ToArray()
        };
        var service = new LayoutService(broken);
        var monitor = new MonitorIdentity("DISPLAY-A", "\\\\.\\DISPLAY1", "Monitor A");

        var active = service.ActiveLayoutFor(monitor);

        Assert.Equal(FirstMonitorWorkId, active.Id);
        Assert.True(active.IsActive);
        Assert.Single(service.LayoutsFor(monitor), layout => layout.IsActive);
    }

    private static SnapConfiguration Configuration()
    {
        var first = new MonitorIdentity("DISPLAY-A", "\\\\.\\DISPLAY1", "Monitor A");
        var second = new MonitorIdentity("DISPLAY-B", "\\\\.\\DISPLAY2", "Monitor B");
        return new SnapConfiguration(
            SnapConfiguration.CurrentSchemaVersion,
            AppSettings.Default(Guid.Empty),
            [
                Layout(FirstMonitorWorkId, "Arbeit", first, true),
                Layout(FirstMonitorGameId, "Gaming", first, false),
                Layout(SecondMonitorWorkId, "Arbeit", second, true),
                Layout(SecondMonitorMovieId, "Film", second, false)
            ]);
    }

    private static MonitorLayout Layout(Guid id, string name, MonitorIdentity monitor, bool isActive) =>
        new(monitor, 2560, 1440, [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)])
        {
            Id = id,
            Name = name,
            IsActive = isActive
        };
}
