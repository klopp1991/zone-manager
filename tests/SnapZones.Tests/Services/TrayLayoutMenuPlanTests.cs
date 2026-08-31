using SnapZones.Presentation.Services;
using SnapZones.Core.Models;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class TrayLayoutMenuPlanTests
{
    [Fact]
    public void Build_groups_layouts_by_monitor_and_marks_each_active_layout()
    {
        var first = new MonitorIdentity("DISPLAY-A", "\\\\.\\DISPLAY1", "Monitor A");
        var second = new MonitorIdentity("DISPLAY-B", "\\\\.\\DISPLAY2", "Monitor B");
        var configuration = new SnapConfiguration(
            SnapConfiguration.CurrentSchemaVersion,
            AppSettings.Default(Guid.Empty),
            [
                Layout("11111111-1111-1111-1111-111111111111", "Arbeit", first, true),
                Layout("22222222-2222-2222-2222-222222222222", "Gaming", first, false),
                Layout("33333333-3333-3333-3333-333333333333", "Standard", second, false),
                Layout("44444444-4444-4444-4444-444444444444", "Film", second, true)
            ]);

        var plan = TrayLayoutMenuPlan.Build(configuration);

        Assert.Equal(["Monitor 1", "Monitor 2"], plan.Monitors.Select(monitor => monitor.Name));
        Assert.Equal(["Arbeit", "Gaming"], plan.Monitors[0].Layouts.Select(layout => layout.Name));
        Assert.True(plan.Monitors[0].Layouts.Single(layout => layout.Name == "Arbeit").IsActive);
        Assert.True(plan.Monitors[1].Layouts.Single(layout => layout.Name == "Film").IsActive);
    }

    [Fact]
    public void Build_uses_custom_monitor_names_without_a_bracketed_identifier()
    {
        var identity = new MonitorIdentity("DISPLAY-A", "\\\\.\\DISPLAY3", "Dell U2723QE");
        var configuration = new SnapConfiguration(
            SnapConfiguration.CurrentSchemaVersion,
            AppSettings.Default(Guid.Empty),
            [Layout("11111111-1111-1111-1111-111111111111", "Arbeit", identity, true)]) with
        {
            MonitorNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["stable:DISPLAY-A"] = "Rechts"
            }
        };

        var plan = TrayLayoutMenuPlan.Build(configuration);

        Assert.Equal("Rechts", Assert.Single(plan.Monitors).Name);
    }

    [Fact]
    public void Build_uses_the_saved_monitor_order_instead_of_the_windows_display_numbers()
    {
        var first = new MonitorIdentity("DISPLAY-A", "\\\\.\\DISPLAY1", "Monitor A");
        var second = new MonitorIdentity("DISPLAY-B", "\\\\.\\DISPLAY2", "Monitor B");
        var configuration = new SnapConfiguration(
            SnapConfiguration.CurrentSchemaVersion,
            AppSettings.Default(Guid.Empty),
            [
                Layout("11111111-1111-1111-1111-111111111111", "Arbeit", first, true),
                Layout("22222222-2222-2222-2222-222222222222", "Gaming", second, true)
            ]) with
        {
            MonitorOrder = ["stable:DISPLAY-B", "stable:DISPLAY-A"]
        };

        var plan = TrayLayoutMenuPlan.Build(configuration);

        Assert.Equal(["Monitor 2", "Monitor 1"], plan.Monitors.Select(monitor => monitor.Name));
    }

    private static MonitorLayout Layout(string id, string name, MonitorIdentity monitor, bool active) =>
        new(monitor, 1920, 1080, [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)])
        {
            Id = Guid.Parse(id),
            Name = name,
            IsActive = active
        };
}
