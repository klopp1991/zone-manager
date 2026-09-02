using SnapZones.Core.Geometry;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Monitors;

/// <summary>
/// Seit dem 02.09.2026 werden Monitore ueber ihre Hardwarekennung wiedererkannt. Vorher galt ein
/// umgesteckter Monitor als neuer Monitor, und seine Layouts blieben als «nicht verbunden» liegen.
/// </summary>
public sealed class MonitorReconciliationTests
{
    private const string OldPath = @"\\?\DISPLAY#GSM9EB9#5&4ace297&1&UID4357#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    private const string NewPath = @"\\?\DISPLAY#GSM9EB9#5&4ace297&1&UID4354#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

    [Fact]
    public void Layouts_of_a_replugged_monitor_are_adopted_by_hardware_id()
    {
        var configuration = ConfigurationOn(new MonitorIdentity(OldPath, @"\\.\DISPLAY1", "LG"));
        configuration = configuration with
        {
            MonitorNames = new Dictionary<string, string> { ["stable:" + OldPath] = "unten 40\"" },
            MonitorOrder = ["stable:" + OldPath]
        };
        var live = Live(new MonitorIdentity(NewPath, @"\\.\DISPLAY2", "LG ULTRAFINE", "GSM9EB9"));

        var result = MonitorReconciliation.Reconcile(configuration, [live]);

        Assert.True(result.Changed);
        Assert.All(result.Configuration.Layouts, layout => Assert.Equal(NewPath, layout.Monitor.StableId));
        Assert.Equal("unten 40\"", result.Configuration.MonitorNames["stable:" + NewPath]);
        Assert.Equal(["stable:" + NewPath], result.Configuration.MonitorOrder);
        Assert.Contains(result.Notices, notice => notice.Contains("übernommen", StringComparison.Ordinal));
    }

    [Fact]
    public void Two_identical_models_without_serial_numbers_are_never_merged()
    {
        var first = new MonitorIdentity(OldPath, @"\\.\DISPLAY1", "LG");
        var configuration = ConfigurationOn(first);
        var liveA = Live(new MonitorIdentity(NewPath, @"\\.\DISPLAY2", "LG", "GSM9EB9"));
        var liveB = Live(new MonitorIdentity(NewPath.Replace("UID4354", "UID4360"), @"\\.\DISPLAY3", "LG", "GSM9EB9"));

        var result = MonitorReconciliation.Reconcile(configuration, [liveA, liveB]);

        Assert.All(result.Configuration.Layouts, layout => Assert.Equal(OldPath, layout.Monitor.StableId));
    }

    [Fact]
    public void A_known_monitor_gets_its_hardware_id_and_current_size_without_notice()
    {
        var identity = new MonitorIdentity(OldPath, @"\\.\DISPLAY1", "LG");
        var configuration = ConfigurationOn(identity);
        var live = new LiveMonitor(identity with { HardwareId = "GSM9EB9#SER123" }, new MonitorWorkArea(0, 0, 5120, 2100), 120, 120, true);

        var result = MonitorReconciliation.Reconcile(configuration, [live]);

        Assert.All(result.Configuration.Layouts, layout =>
        {
            Assert.Equal("GSM9EB9#SER123", layout.Monitor.HardwareId);
            Assert.Equal(5120, layout.SavedWidth);
            Assert.Equal(2100, layout.SavedHeight);
        });
        Assert.Single(result.Notices, notice => notice.Contains("Arbeitsfläche", StringComparison.Ordinal));
    }

    [Fact]
    public void Orphaned_names_and_order_entries_are_removed()
    {
        var identity = new MonitorIdentity(OldPath, @"\\.\DISPLAY1", "LG");
        var configuration = ConfigurationOn(identity) with
        {
            MonitorNames = new Dictionary<string, string>
            {
                ["stable:" + OldPath] = "unten",
                [@"stable:\\?\DISPLAY#HWP3264#5&4ace297&1&UID4354#{x}"] = "oben"
            },
            MonitorOrder = [@"stable:\\?\DISPLAY#HWP3264#5&4ace297&1&UID4354#{x}", "stable:" + OldPath]
        };

        var result = MonitorReconciliation.Reconcile(configuration, [Live(identity)]);

        Assert.Equal(["stable:" + OldPath], result.Configuration.MonitorNames.Keys);
        Assert.Equal(["stable:" + OldPath], result.Configuration.MonitorOrder);
        Assert.Contains(result.Notices, notice => notice.Contains("bereinigt", StringComparison.Ordinal));
    }

    [Fact]
    public void Hardware_id_is_derived_from_the_device_path()
    {
        Assert.Equal("GSM9EB9", MonitorHardwareId.FromDevicePath(OldPath));
        Assert.Equal(string.Empty, MonitorHardwareId.FromDevicePath(@"\\.\DISPLAY1"));
        Assert.Equal("GSM9EB9#ABC", MonitorHardwareId.Compose("gsm9eb9", " ABC "));
        Assert.Equal("GSM9EB9", MonitorHardwareId.ModelOf("GSM9EB9#ABC"));
    }

    private static SnapConfiguration ConfigurationOn(MonitorIdentity monitor)
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        return configuration with
        {
            Layouts = configuration.Layouts.Select(layout => layout with { Monitor = monitor }).ToArray()
        };
    }

    private static LiveMonitor Live(MonitorIdentity identity) =>
        new(identity, new MonitorWorkArea(0, 0, 3440, 1440), 96, 96, true);
}
