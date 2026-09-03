using SnapZones.Core.Models;
using SnapZones.Core.Persistence;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Persistence;

/// <summary>
/// Schema 7 (03.09.2026): Strg + Alt war als Voreinstellung der Zonenkuerzel unbrauchbar, weil Windows
/// AltGr intern als Strg + Alt liefert und ein globales Kuerzel damit jedes AltGr-Zeichen auf derselben
/// Taste verschluckt — auf einer Schweizer Tastatur unter anderem das @. Ein bestehender Stand wird
/// einmalig umgestellt; eine spaetere bewusste Wahl bleibt stehen.
/// </summary>
public sealed class ZoneHotkeyModifierMigrationTests
{
    [Fact]
    public async Task An_older_configuration_moves_off_control_alt()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var configuration = ConfigurationSamples.TwoLayouts() with
        {
            SchemaVersion = 6,
            Settings = ConfigurationSamples.TwoLayouts().Settings with
            {
                ZoneHotkeyModifiers = ZoneHotkeyModifiers.ControlAlt
            }
        };
        await WriteAsync(directory.Path, configuration);

        var loaded = await repository.LoadAsync(CancellationToken.None);

        Assert.Equal(SnapConfiguration.CurrentSchemaVersion, loaded.Configuration.SchemaVersion);
        Assert.Equal(ZoneHotkeyModifiers.ControlShift, loaded.Configuration.Settings.ZoneHotkeyModifiers);
    }

    [Fact]
    public async Task A_deliberate_choice_of_control_alt_survives()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var configuration = ConfigurationSamples.TwoLayouts();
        configuration = configuration with
        {
            Settings = configuration.Settings with { ZoneHotkeyModifiers = ZoneHotkeyModifiers.ControlAlt }
        };

        await repository.SaveAsync(configuration, CancellationToken.None);
        var loaded = await repository.LoadAsync(CancellationToken.None);

        Assert.Equal(ZoneHotkeyModifiers.ControlAlt, loaded.Configuration.Settings.ZoneHotkeyModifiers);
    }

    [Fact]
    public async Task Another_choice_in_an_older_configuration_is_not_touched()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var sample = ConfigurationSamples.TwoLayouts();
        var configuration = sample with
        {
            SchemaVersion = 6,
            Settings = sample.Settings with { ZoneHotkeyModifiers = ZoneHotkeyModifiers.AltShift }
        };
        await WriteAsync(directory.Path, configuration);

        var loaded = await repository.LoadAsync(CancellationToken.None);

        Assert.Equal(ZoneHotkeyModifiers.AltShift, loaded.Configuration.Settings.ZoneHotkeyModifiers);
    }

    /// <summary>
    /// Schreibt die Datei an <see cref="JsonConfigurationRepository.SaveAsync"/> vorbei: dort gilt die
    /// Pruefung, die nur die aktuelle Schemaversion durchlaesst.
    /// </summary>
    private static Task WriteAsync(string directoryPath, SnapConfiguration configuration) =>
        File.WriteAllTextAsync(
            Path.Combine(directoryPath, "settings.json"),
            System.Text.Json.JsonSerializer.Serialize(configuration, SerializerOptions));

    private static readonly System.Text.Json.JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private static System.Text.Json.JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return options;
    }
}
