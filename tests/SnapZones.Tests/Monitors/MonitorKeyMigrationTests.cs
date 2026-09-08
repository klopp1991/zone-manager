using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.Persistence;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Monitors;

/// <summary>
/// Seit dem 08.09.2026 haengen Name, Reihenfolge und Monitorkombination an der Hardwarekennung des
/// Monitors statt an seinem Anzeigepfad. Der Anzeigepfad fuehrt Grafikkarte und Anschluss mit: nach
/// einem Wechsel des Anschlusses verlor derselbe Monitor seinen Namen und trat unter der von Windows
/// neu vergebenen Anzeigenummer wieder auf («Monitor 6»), waehrend der alte Name als verwaister
/// Eintrag liegen blieb.
/// </summary>
public sealed class MonitorKeyMigrationTests
{
    // So lagen die Kennungen desselben Dell U5226KW am 08.09.2026 in der Konfiguration: einmal am
    // fruehreren Anschluss, einmal am jetzigen. Die Seriennummer ist in beiden Faellen dieselbe.
    private const string FormerPath = @"\\?\DISPLAY#DEL43A0#1&8713bca&0&UID0#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    private const string CurrentPath = @"\\?\DISPLAY#DEL43A0#5&4ace297&1&UID4357#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    private const string HardwareId = "DEL43A0#8ZHYPF4";
    private const string HardwareKey = "hw:" + HardwareId;

    [Fact]
    public void The_key_of_a_monitor_with_a_serial_number_is_its_hardware_id()
    {
        var atFormerPort = new MonitorIdentity(FormerPath, @"\\.\DISPLAY5", "DELL U5226KW", HardwareId);
        var atCurrentPort = new MonitorIdentity(CurrentPath, @"\\.\DISPLAY6", "DELL U5226KW", HardwareId);

        Assert.Equal(HardwareKey, MonitorNaming.KeyFor(atFormerPort));
        Assert.Equal(MonitorNaming.KeyFor(atFormerPort), MonitorNaming.KeyFor(atCurrentPort));
        Assert.True(LayoutService.BelongsToMonitor(atFormerPort, atCurrentPort));
    }

    [Fact]
    public void Without_a_serial_number_the_display_path_stays_the_key()
    {
        // Zwei baugleiche Monitore ohne Seriennummer sind nur ueber den Anschluss zu unterscheiden.
        var first = new MonitorIdentity(CurrentPath, @"\\.\DISPLAY1", "LG", "GSM9EB9");
        var second = new MonitorIdentity(FormerPath, @"\\.\DISPLAY2", "LG", "GSM9EB9");

        Assert.Equal("stable:" + CurrentPath, MonitorNaming.KeyFor(first));
        Assert.NotEqual(MonitorNaming.KeyFor(first), MonitorNaming.KeyFor(second));
        Assert.False(LayoutService.BelongsToMonitor(first, second));
    }

    [Fact]
    public void Two_monitors_on_the_same_port_are_told_apart_by_their_serial_numbers()
    {
        // Derselbe Anschluss, nacheinander mit zwei Monitoren belegt: der Anzeigepfad allein wuerde
        // beide zusammenwerfen, sobald das Modellsegment gleich ist.
        var dell = new MonitorIdentity(CurrentPath, @"\\.\DISPLAY1", "DELL U5226KW", HardwareId);
        var other = new MonitorIdentity(CurrentPath, @"\\.\DISPLAY1", "DELL U5226KW", "DEL43A0#4XYZ001");

        Assert.False(LayoutService.BelongsToMonitor(dell, other));
    }

    [Fact]
    public void A_name_given_at_the_former_port_survives_the_move()
    {
        var configuration = ConfigurationWithBothPorts() with
        {
            MonitorNames = new Dictionary<string, string> { ["stable:" + FormerPath] = "52\"" },
            MonitorOrder = ["stable:" + FormerPath]
        };

        var migrated = MonitorKeyMigration.Apply(configuration);

        Assert.Equal("52\"", migrated.MonitorNames[HardwareKey]);
        Assert.Equal([HardwareKey], migrated.MonitorOrder);
        Assert.Equal(
            "52\"",
            MonitorNaming.CustomNameFor(migrated, new MonitorIdentity(CurrentPath, @"\\.\DISPLAY6", "DELL U5226KW", HardwareId)));
    }

    [Fact]
    public void Two_names_for_the_same_monitor_collapse_into_one_entry()
    {
        var configuration = ConfigurationWithBothPorts() with
        {
            MonitorNames = new Dictionary<string, string>
            {
                ["stable:" + FormerPath] = "52\"",
                ["stable:" + CurrentPath] = "Monitor 6"
            }
        };

        var migrated = MonitorKeyMigration.Apply(configuration);

        Assert.Equal([HardwareKey], migrated.MonitorNames.Keys);
        Assert.Equal("52\"", migrated.MonitorNames[HardwareKey]);
    }

    [Fact]
    public void Monitor_sets_follow_the_new_key()
    {
        var configuration = ConfigurationWithBothPorts();
        var layoutId = configuration.Layouts[0].Id;
        configuration = configuration with
        {
            MonitorSets =
            [
                new MonitorSetSelection(
                    "stable:" + FormerPath,
                    new Dictionary<string, Guid> { ["stable:" + FormerPath] = layoutId })
            ]
        };

        var migrated = MonitorKeyMigration.Apply(configuration);

        var set = Assert.Single(migrated.MonitorSets);
        Assert.Equal(HardwareKey, set.SetKey);
        Assert.Equal(layoutId, set.ActiveLayouts[HardwareKey]);
    }

    [Fact]
    public void Running_the_migration_twice_changes_nothing()
    {
        var configuration = ConfigurationWithBothPorts() with
        {
            MonitorNames = new Dictionary<string, string> { ["stable:" + FormerPath] = "52\"" },
            MonitorOrder = ["stable:" + FormerPath]
        };

        var once = MonitorKeyMigration.Apply(configuration);
        var twice = MonitorKeyMigration.Apply(once);

        Assert.Equal(once.MonitorNames, twice.MonitorNames);
        Assert.Equal(once.MonitorOrder, twice.MonitorOrder);
    }

    [Fact]
    public async Task Loading_a_schema_seven_configuration_moves_the_name_to_the_hardware_key()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var configuration = ConfigurationWithBothPorts() with
        {
            MonitorNames = new Dictionary<string, string> { ["stable:" + FormerPath] = "52\"" }
        };
        await repository.SaveAsync(configuration, CancellationToken.None);
        var settingsPath = Path.Combine(directory.Path, "settings.json");
        await File.WriteAllTextAsync(
            settingsPath,
            (await File.ReadAllTextAsync(settingsPath)).Replace(
                $"\"SchemaVersion\": {SnapConfiguration.CurrentSchemaVersion}",
                "\"SchemaVersion\": 7",
                StringComparison.Ordinal));

        var loaded = await new JsonConfigurationRepository(directory.Path).LoadAsync(CancellationToken.None);

        Assert.Equal(SnapConfiguration.CurrentSchemaVersion, loaded.Configuration.SchemaVersion);
        Assert.Equal("52\"", loaded.Configuration.MonitorNames[HardwareKey]);
    }

    /// <summary>Zwei Layouts desselben Monitors, aufgezeichnet an zwei verschiedenen Anschluessen.</summary>
    private static SnapConfiguration ConfigurationWithBothPorts()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var layouts = configuration.Layouts.ToArray();
        layouts[0] = layouts[0] with
        {
            Monitor = new MonitorIdentity(FormerPath, @"\\.\DISPLAY5", "DELL U5226KW", HardwareId)
        };
        layouts[1] = layouts[1] with
        {
            Monitor = new MonitorIdentity(CurrentPath, @"\\.\DISPLAY1", "DELL U5226KW", HardwareId)
        };
        return configuration with { Layouts = layouts };
    }
}
