using System.Text.Json;
using SnapZones.Core.Editor;
using SnapZones.Core.Models;
using SnapZones.Core.Persistence;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Editor;

/// <summary>
/// Das Kennzeichen «virtueller Monitor» einer Zone muss gespeichert, im Editor umschaltbar und
/// rueckgaengig machbar sein und in der Sicherungsuebersicht benannt werden. Eine gewoehnliche Zone
/// schreibt das Feld nicht, damit aeltere Staende unveraendert bleiben.
/// </summary>
public sealed class VirtualZoneDefinitionTests
{
    [Fact]
    public void The_flag_round_trips_through_json_and_is_omitted_when_false()
    {
        var plain = new ZoneDefinition(Guid.NewGuid(), "Links", NormalizedRect.Full);
        var virtualZone = plain with { IsFullscreenZone = true };

        var plainJson = JsonSerializer.Serialize(plain);
        var virtualJson = JsonSerializer.Serialize(virtualZone);

        Assert.DoesNotContain("IsFullscreenZone", plainJson);
        Assert.Contains("\"IsFullscreenZone\":true", virtualJson);
        Assert.True(JsonSerializer.Deserialize<ZoneDefinition>(virtualJson)!.IsFullscreenZone);
        Assert.False(JsonSerializer.Deserialize<ZoneDefinition>(plainJson)!.IsFullscreenZone);
    }

    [Fact]
    public async Task The_flag_survives_saving_and_loading_the_configuration()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var layout = configuration.Layouts[0];
        var marked = layout with { Zones = [layout.Zones[0] with { IsFullscreenZone = true }, .. layout.Zones.Skip(1)] };
        configuration = configuration with { Layouts = [marked, .. configuration.Layouts.Skip(1)] };

        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        await repository.SaveAsync(configuration, CancellationToken.None);
        var loaded = await repository.LoadAsync(CancellationToken.None);

        var loadedLayout = loaded.Configuration.Layouts.Single(candidate => candidate.Id == layout.Id);
        Assert.True(loadedLayout.Zones[0].IsFullscreenZone);
        Assert.All(loadedLayout.Zones.Skip(1), zone => Assert.False(zone.IsFullscreenZone));
    }

    [Fact]
    public void The_editor_toggles_the_flag_and_undo_takes_it_back()
    {
        var layout = ConfigurationSamples.TwoLayouts().Layouts[0];
        var session = new LayoutEditorSession(layout);
        var zoneId = layout.Zones[0].Id;

        session.SetVirtualMonitor(zoneId, true);
        Assert.True(session.Zones.Single(zone => zone.Id == zoneId).IsFullscreenZone);
        Assert.True(session.IsDirty);

        session.SetVirtualMonitor(zoneId, true);
        Assert.True(session.Undo());
        Assert.False(session.Zones.Single(zone => zone.Id == zoneId).IsFullscreenZone);
        Assert.False(session.IsDirty);

        Assert.True(session.Redo());
        Assert.True(session.CreateSnapshot().Zones.Single(zone => zone.Id == zoneId).IsFullscreenZone);
        Assert.Throws<KeyNotFoundException>(() => session.SetVirtualMonitor(Guid.NewGuid(), true));
    }

    [Fact]
    public void The_backup_summary_names_the_change()
    {
        var older = ConfigurationSamples.TwoLayouts();
        var layout = older.Layouts[0];
        var newer = older with
        {
            Layouts = [layout with { Zones = [layout.Zones[0] with { IsFullscreenZone = true }, .. layout.Zones.Skip(1)] }, .. older.Layouts.Skip(1)]
        };

        Assert.Equal($"Layout «{layout.Name}»: Zone «{layout.Zones[0].Name}» als virtueller Monitor gekennzeichnet", ConfigurationDiff.Summarize(older, newer));
        Assert.Equal($"Layout «{layout.Name}»: Zone «{layout.Zones[0].Name}» ist kein virtueller Monitor mehr", ConfigurationDiff.Summarize(newer, older));
    }
}
