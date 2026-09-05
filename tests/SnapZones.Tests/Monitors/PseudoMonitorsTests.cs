using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.Persistence;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Monitors;

/// <summary>
/// Gesperrte Sitzung und ausgeschaltete Monitore lassen Windows Platzhalteranzeigen melden. Sie duerfen
/// weder als Monitor gelten noch in der Konfiguration liegen bleiben; sonst erscheinen in der
/// Oberflaeche «Monitor 1» und «Monitor 3», die es nicht gibt.
/// </summary>
public sealed class PseudoMonitorsTests
{
    private static readonly MonitorIdentity WinDisc = new("WinDisc", "WinDisc", "WinDisc");
    private static readonly MonitorIdentity DefaultMonitor = new(
        "\\\\?\\DISPLAY#Default_Monitor#1&c528b8a&5&UID256#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}",
        "\\\\.\\DISPLAY1",
        "Generic PnP Monitor",
        "DEFAULT_MONITOR");
    private static readonly MonitorIdentity Real = new("\\\\?\\DISPLAY#GSM9EB9#5&4ace297&1&UID4357#{guid}", "\\\\.\\DISPLAY1", "LG ULTRAFINE", "GSM9EB9#602NTSUJC086");

    [Fact]
    public void Windows_placeholders_are_recognised_and_real_monitors_are_not()
    {
        Assert.True(PseudoMonitors.IsPseudo(WinDisc));
        Assert.True(PseudoMonitors.IsPseudo(DefaultMonitor));
        Assert.False(PseudoMonitors.IsPseudo(Real));

        // Ein echter Monitor ohne EDID heisst zwar «Generic PnP Monitor», hat aber einen eigenen Anzeigepfad.
        Assert.False(PseudoMonitors.IsPseudo(new MonitorIdentity("\\\\?\\DISPLAY#ABC1234#4&1#{guid}", "\\\\.\\DISPLAY2", "Generic PnP Monitor", "ABC1234")));
    }

    [Fact]
    public void Only_real_monitors_survive_the_filter()
    {
        var monitors = new[]
        {
            new LiveMonitor(WinDisc, new MonitorWorkArea(0, 0, 5137, 3540), 96, 96, true),
            new LiveMonitor(Real, new MonitorWorkArea(0, 0, 5120, 2088), 144, 144, true)
        };

        Assert.Equal([Real], PseudoMonitors.RealOnly(monitors).Select(monitor => monitor.Identity));
        Assert.Empty(PseudoMonitors.RealOnly([monitors[0]]));
    }

    [Fact]
    public async Task Loading_drops_layouts_names_and_sets_of_placeholders_but_keeps_everything_else()
    {
        var real = new MonitorLayout(Real, 5120, 2088, [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)]) { Name = "Arbeiten" };
        var phantomA = new MonitorLayout(WinDisc, 5137, 3540, [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)]);
        var phantomB = new MonitorLayout(DefaultMonitor, 2558, 1278, [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)]);
        var configuration = SnapConfiguration.CreateDefault() with
        {
            Layouts = [real, phantomA, phantomB],
            MonitorNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [MonitorNaming.KeyFor(Real)] = "unten 40\"",
                [MonitorNaming.KeyFor(WinDisc)] = "Geist"
            },
            MonitorOrder = [MonitorNaming.KeyFor(WinDisc), MonitorNaming.KeyFor(Real)],
            MonitorSets =
            [
                new MonitorSetSelection("stable:WinDisc", new Dictionary<string, Guid> { [MonitorNaming.KeyFor(WinDisc)] = phantomA.Id }),
                new MonitorSetSelection("hw:DEFAULT_MONITOR", new Dictionary<string, Guid> { [MonitorNaming.KeyFor(DefaultMonitor)] = phantomB.Id }),
                new MonitorSetSelection("hw:GSM9EB9#602NTSUJC086", new Dictionary<string, Guid> { [MonitorNaming.KeyFor(Real)] = real.Id })
            ]
        };

        var pruned = PseudoMonitors.Prune(configuration);

        Assert.Equal([real.Id], pruned.Layouts.Select(layout => layout.Id));
        Assert.Equal(["unten 40\""], pruned.MonitorNames.Values);
        Assert.Equal([MonitorNaming.KeyFor(Real)], pruned.MonitorOrder);
        Assert.Equal(["hw:GSM9EB9#602NTSUJC086"], pruned.MonitorSets.Select(set => set.SetKey));
        Assert.Same(pruned, PseudoMonitors.Prune(pruned));

        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        await repository.SaveAsync(configuration, CancellationToken.None);
        var loaded = await repository.LoadAsync(CancellationToken.None);

        Assert.Equal([real.Id], loaded.Configuration.Layouts.Select(layout => layout.Id));
    }

    [Fact]
    public void The_reconciliation_ignores_placeholders_that_arrive_with_the_live_monitors()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var monitors = new[]
        {
            new LiveMonitor(WinDisc, new MonitorWorkArea(0, 0, 5137, 3540), 96, 96, true)
        };

        var result = MonitorReconciliation.Reconcile(configuration, monitors);

        Assert.Equal(2, result.Configuration.Layouts.Count);
        Assert.DoesNotContain(result.Configuration.Layouts, layout => PseudoMonitors.IsPseudo(layout.Monitor));
    }
}
