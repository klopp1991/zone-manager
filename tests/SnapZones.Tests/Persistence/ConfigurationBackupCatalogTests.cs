using SnapZones.Core.AppRules;
using SnapZones.Core.Models;
using SnapZones.Core.Persistence;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Persistence;

/// <summary>
/// Die fuenf automatischen Sicherungen werden auf der Seite «Programm» als «Fruehere Staende» gezeigt. Jeder
/// Stand traegt einen Satz, was sich danach geaendert hat; sonst faende niemand den richtigen wieder.
/// </summary>
public sealed class ConfigurationBackupCatalogTests
{
    [Fact]
    public async Task Saving_twice_lists_the_previous_state_with_what_changed_afterwards()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var catalog = new ConfigurationBackupCatalog(directory.Path);
        var first = ConfigurationSamples.TwoLayouts();
        var second = first with { Settings = first.Settings with { OverlayOpacity = 0.30 } };

        await repository.SaveAsync(first, CancellationToken.None);
        Assert.Empty(catalog.List(first));

        await repository.SaveAsync(second, CancellationToken.None);
        var backups = catalog.List(second);

        var backup = Assert.Single(backups);
        Assert.True(backup.IsReadable);
        Assert.Equal("Deckkraft 24 % → 30 %", backup.Summary);
        Assert.InRange(backup.SavedAt, DateTimeOffset.Now.AddMinutes(-5), DateTimeOffset.Now.AddMinutes(1));

        var restored = await catalog.LoadAsync(backup.Path, CancellationToken.None);
        Assert.Equal(0.24, restored.Settings.OverlayOpacity, 3);
    }

    [Fact]
    public void An_unreadable_backup_is_listed_but_cannot_be_restored()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Path, "settings.backup-1.json"), "{ kaputt");
        var catalog = new ConfigurationBackupCatalog(directory.Path);

        var backup = Assert.Single(catalog.List(ConfigurationSamples.TwoLayouts()));

        Assert.False(backup.IsReadable);
        Assert.Equal("Sicherung nicht lesbar", backup.Summary);
    }

    [Fact]
    public void The_diff_names_the_first_visible_difference_in_plain_words()
    {
        var older = ConfigurationSamples.TwoLayouts();
        var layout = older.Layouts[0];
        var rule = new AppRule(Guid.NewGuid(), "Explorer.exe", null, null, AppRuleEvent.WindowCreated, 0, 0, 50, true, layout.Id, layout.Zones[1].Id);

        Assert.Equal(ConfigurationDiff.Unchanged, ConfigurationDiff.Summarize(older, null));
        Assert.Equal(ConfigurationDiff.Unchanged, ConfigurationDiff.Summarize(older, older));
        Assert.Equal("Zuordnung Explorer.exe → Rechts angelegt", ConfigurationDiff.Summarize(older, older with { AppRules = [rule] }));
        Assert.Equal("Layout «Video» angelegt", ConfigurationDiff.Summarize(older, older with { Layouts = [.. older.Layouts, layout with { Id = Guid.NewGuid(), Name = "Video", IsActive = false }] }));
        Assert.Equal("Layout «Abend» gelöscht", ConfigurationDiff.Summarize(older, older with { Layouts = [layout] }));
        Assert.Equal("Layout «Arbeit»: Zone 1 verkleinert", ConfigurationDiff.Summarize(
            older,
            older with { Layouts = [layout with { Zones = [layout.Zones[0] with { Bounds = new NormalizedRect(0, 0, 0.4, 1) }, layout.Zones[1]] }, older.Layouts[1]] }));
        Assert.Equal("«notepad.exe» wird in Ruhe gelassen", ConfigurationDiff.Summarize(
            older,
            older with { AppExclusions = [new AppExclusion(Guid.NewGuid(), "notepad.exe", null, null, true)] }));
        Assert.Equal("Autostart eingeschaltet", ConfigurationDiff.Summarize(older, older with { Settings = older.Settings with { StartWithWindows = true } }));

        // Eine reine Oberflaechenvorliebe zaehlt nicht als Aenderung des Stands.
        Assert.Equal(ConfigurationDiff.Unchanged, ConfigurationDiff.Summarize(older, older with { Settings = older.Settings with { EditorValuePanelOpen = false } }));
    }
}
