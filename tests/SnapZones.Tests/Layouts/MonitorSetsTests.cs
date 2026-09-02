using SnapZones.Core.Geometry;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Layouts;

public sealed class MonitorSetsTests
{
    [Fact]
    public void The_set_key_ignores_order_and_prefers_hardware_ids()
    {
        var laptop = Live(new MonitorIdentity("path-a", @"\\.\DISPLAY1", "Laptop", "LEN1234#S1"));
        var dock = Live(new MonitorIdentity("path-b", @"\\.\DISPLAY2", "Dell", "DEL5678"));

        Assert.Equal(MonitorSets.KeyFor([laptop, dock]), MonitorSets.KeyFor([dock, laptop]));
        Assert.Equal("hw:DEL5678+hw:LEN1234#S1", MonitorSets.KeyFor([laptop, dock]));
        Assert.NotEqual(MonitorSets.KeyFor([laptop]), MonitorSets.KeyFor([laptop, dock]));
    }

    [Fact]
    public void Recording_and_applying_restores_the_layout_chosen_for_a_combination()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var monitor = Live(configuration.Layouts[0].Monitor);
        var docked = MonitorSets.KeyFor([monitor]);

        // Am Dock wird «Abend» gewaehlt und gemerkt.
        var evening = new LayoutService(configuration);
        evening.ActivateLayout(configuration.Layouts[1].Id);
        var recorded = MonitorSets.Record(evening.Configuration, docked, [monitor]);
        Assert.Single(recorded.MonitorSets);

        // Spaeter ist wieder «Arbeit» aktiv (etwa unterwegs gewechselt); das Dock stellt «Abend» her.
        var elsewhere = new LayoutService(recorded);
        elsewhere.ActivateLayout(configuration.Layouts[0].Id);
        var applied = MonitorSets.Apply(elsewhere.Configuration, docked, [monitor], out var activated);

        Assert.Equal("Abend", Assert.Single(activated).Name);
        Assert.True(applied.Layouts.Single(layout => layout.Name == "Abend").IsActive);
        Assert.False(applied.Layouts.Single(layout => layout.Name == "Arbeit").IsActive);
    }

    [Fact]
    public void Applying_an_unknown_combination_changes_nothing()
    {
        var configuration = ConfigurationSamples.TwoLayouts();

        var applied = MonitorSets.Apply(configuration, "hw:UNKNOWN", [Live(configuration.Layouts[0].Monitor)], out var activated);

        Assert.Same(configuration, applied);
        Assert.Empty(activated);
    }

    [Fact]
    public void Pruning_drops_references_to_deleted_layouts()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var sets = new[]
        {
            new MonitorSetSelection("a", new Dictionary<string, Guid> { ["m"] = configuration.Layouts[0].Id }),
            new MonitorSetSelection("b", new Dictionary<string, Guid> { ["m"] = Guid.NewGuid() })
        };

        var pruned = MonitorSets.Prune(sets, configuration.Layouts);

        Assert.Equal("a", Assert.Single(pruned).SetKey);
    }

    private static LiveMonitor Live(MonitorIdentity identity) =>
        new(identity, new MonitorWorkArea(0, 0, 3440, 1440), 96, 96, true);
}
