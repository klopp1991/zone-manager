using SnapZones.Core.Models;
using SnapZones.Core.Persistence;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Persistence;

/// <summary>
/// Die Hauptzone ist ein zusätzliches, weglassbares Feld am Layout. Bestehende Stände laufen ohne sie
/// weiter; ein Verweis ins Leere oder eine zweite Hauptzone darf nie in den Betrieb gelangen.
/// </summary>
public sealed class MainZonePersistenceTests
{
    private static readonly Guid WorkLayoutId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EveningLayoutId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LeftZoneId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid VideoZoneId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task A_configuration_without_a_main_zone_loads_unchanged()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        await repository.SaveAsync(ConfigurationSamples.TwoLayouts(), CancellationToken.None);

        var loaded = await repository.LoadAsync(CancellationToken.None);

        Assert.All(loaded.Configuration.Layouts, layout => Assert.Null(layout.MainZoneId));
    }

    [Fact]
    public async Task The_main_zone_survives_a_save_and_load_round_trip()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);

        await repository.SaveAsync(WithMainZone(WorkLayoutId, LeftZoneId), CancellationToken.None);
        var loaded = await repository.LoadAsync(CancellationToken.None);

        Assert.Equal(LeftZoneId, loaded.Configuration.Layouts.Single(layout => layout.Id == WorkLayoutId).MainZoneId);
        Assert.Null(loaded.Configuration.Layouts.Single(layout => layout.Id == EveningLayoutId).MainZoneId);
    }

    [Fact]
    public async Task A_main_zone_pointing_at_a_missing_zone_is_rejected()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => repository.SaveAsync(WithMainZone(WorkLayoutId, VideoZoneId), CancellationToken.None));
    }

    [Fact]
    public async Task More_than_one_main_zone_is_rejected()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var configuration = ConfigurationSamples.TwoLayouts();
        configuration = configuration with
        {
            Layouts = configuration.Layouts
                .Select(layout => layout.Id == WorkLayoutId
                    ? layout with { MainZoneId = LeftZoneId }
                    : layout with { MainZoneId = VideoZoneId })
                .ToArray()
        };

        await Assert.ThrowsAsync<InvalidDataException>(
            () => repository.SaveAsync(configuration, CancellationToken.None));
    }

    private static SnapConfiguration WithMainZone(Guid layoutId, Guid zoneId)
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        return configuration with
        {
            Layouts = configuration.Layouts
                .Select(layout => layout.Id == layoutId ? layout with { MainZoneId = zoneId } : layout)
                .ToArray()
        };
    }
}
