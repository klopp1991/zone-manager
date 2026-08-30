using SnapZones.App.ViewModels;
using SnapZones.Core.Editor;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.Placement;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.ViewModels;

public sealed class MainViewModelPersistenceTests
{
    [Fact]
    public void Placement_rule_change_requests_persistence_of_the_exact_rules()
    {
        var viewModel = CreateViewModel();
        SnapConfiguration? requested = null;
        viewModel.SaveRequested += configuration => requested = configuration;
        var identity = new WindowIdentity("editor.exe", "EditorMain", WindowKind.MainWindow);
        viewModel.WindowPlacement.ReplaceCatalog(new(WindowPlacementCatalog.CurrentSchemaVersion, [
            new WindowPlacementEntry(
                identity,
                viewModel.SelectedMonitor!.Live.Identity.StableId,
                viewModel.Editor!.Zones[0].Id,
                viewModel.SelectedMonitor.Live.WorkArea,
                new PixelRect(0, 0, 800, 600),
                NormalizedRect.Full,
                false,
                DateTimeOffset.UtcNow)
        ]));
        viewModel.WindowPlacement.SelectedItem = viewModel.WindowPlacement.Items[0];

        viewModel.WindowPlacement.ExcludeSelected();

        var rule = Assert.Single(requested!.Settings.EffectiveWindowPlacementRules);
        Assert.Equal(WindowPlacementMode.Exclude, rule.Action);
        Assert.Equal(viewModel.WindowPlacement.SelectedItem.Identity.ApplicationKey, rule.ApplicationKey);
    }

    [Fact]
    public void Layout_change_refreshes_window_placement_zones_from_the_current_profile_snapshot()
    {
        var viewModel = CreateViewModel();

        viewModel.Editor!.ApplyTemplate(LayoutTemplate.ThreeColumns);

        Assert.Equal(3, viewModel.WindowPlacement.TargetZones.Count);
        Assert.Equal(viewModel.Editor.Zones.Select(zone => zone.Id), viewModel.WindowPlacement.TargetZones.Select(zone => zone.Id));
    }

    [Fact]
    public void Layout_change_rebinds_selected_profile_to_the_replacement_instance_with_the_same_id()
    {
        var viewModel = CreateViewModel();
        var previous = viewModel.SelectedProfile;

        viewModel.Editor!.ApplyTemplate(LayoutTemplate.ThreeColumns);

        var refreshed = viewModel.Profiles.Single(profile => profile.Id == previous.Id);
        Assert.NotSame(previous, refreshed);
        Assert.Same(refreshed, viewModel.SelectedProfile);
    }

    [Fact]
    public void Imported_title_specific_rules_remain_distinct_when_one_selector_is_edited()
    {
        var viewModel = CreateViewModel();
        var identity = new WindowIdentity("editor.exe", "EditorMain", WindowKind.MainWindow);
        var report = new WindowPlacementRule(
            Guid.NewGuid(), true, identity.ApplicationKey, identity.WindowClass, identity.Kind, "Report*",
            WindowPlacementMode.RememberLast, null, null, null);
        var invoice = report with { Id = Guid.NewGuid(), TitlePattern = "Invoice*", Action = WindowPlacementMode.Exclude };
        var imported = viewModel.Configuration with
        {
            Settings = viewModel.Configuration.Settings with { WindowPlacementRules = [report, invoice] }
        };
        viewModel.ReplaceConfiguration(imported);
        viewModel.WindowPlacement.ReplaceCatalog(new(WindowPlacementCatalog.CurrentSchemaVersion, [
            new WindowPlacementEntry(
                identity,
                viewModel.SelectedMonitor!.Live.Identity.StableId,
                viewModel.Editor!.Zones[0].Id,
                viewModel.SelectedMonitor.Live.WorkArea,
                new PixelRect(0, 0, 800, 600),
                NormalizedRect.Full,
                false,
                DateTimeOffset.UtcNow)
        ]));
        viewModel.WindowPlacement.SelectedItem = viewModel.WindowPlacement.Items[0];
        viewModel.WindowPlacement.TitlePattern = "Report*";

        viewModel.WindowPlacement.ExcludeSelected();

        Assert.Equal(2, viewModel.WindowPlacement.Rules.Count);
        Assert.Equal(report.Id, viewModel.WindowPlacement.Rules.Single(rule => rule.TitlePattern == "Report*").Id);
        Assert.Equal(invoice.Id, viewModel.WindowPlacement.Rules.Single(rule => rule.TitlePattern == "Invoice*").Id);
    }

    [Fact]
    public void Replacing_configuration_reloads_the_selected_placement_editor_from_the_single_imported_rule()
    {
        var viewModel = CreateViewModel();
        var identity = new WindowIdentity("editor.exe", "EditorMain", WindowKind.MainWindow);
        viewModel.WindowPlacement.ReplaceCatalog(new(WindowPlacementCatalog.CurrentSchemaVersion, [
            new WindowPlacementEntry(
                identity,
                viewModel.SelectedMonitor!.Live.Identity.StableId,
                viewModel.Editor!.Zones[0].Id,
                viewModel.SelectedMonitor.Live.WorkArea,
                new PixelRect(0, 0, 800, 600),
                NormalizedRect.Full,
                false,
                DateTimeOffset.UtcNow)
        ]));
        viewModel.WindowPlacement.SelectedItem = viewModel.WindowPlacement.Items[0];
        viewModel.WindowPlacement.TitlePattern = "Ungespeichert*";
        var imported = ConfigurationSamples.TwoProfiles();
        var targetProfile = imported.Profiles[1];
        var targetMonitor = targetProfile.Monitors[0];
        var targetZone = targetMonitor.Zones[1];
        var importedRule = new WindowPlacementRule(
            Guid.NewGuid(),
            true,
            identity.ApplicationKey,
            identity.WindowClass,
            identity.Kind,
            "Importiert*",
            WindowPlacementMode.FixedZone,
            targetProfile.Id,
            targetMonitor.Monitor.StableId,
            targetZone.Id);
        imported = imported with
        {
            Settings = imported.Settings with { WindowPlacementRules = [importedRule] }
        };

        viewModel.ReplaceConfiguration(imported);

        Assert.Equal(identity, viewModel.WindowPlacement.SelectedItem!.Identity);
        Assert.Equal("Importiert*", viewModel.WindowPlacement.TitlePattern);
        Assert.Equal(WindowPlacementMode.FixedZone, viewModel.WindowPlacement.SelectedRuleMode);
        Assert.Equal(targetProfile.Id, viewModel.WindowPlacement.SelectedTargetProfile!.Id);
        Assert.Equal(targetMonitor.Monitor.StableId, viewModel.WindowPlacement.SelectedTargetMonitor!.Live.Identity.StableId);
        Assert.Equal(targetZone.Id, viewModel.WindowPlacement.SelectedTargetZone!.Id);
    }

    [Fact]
    public void Valid_layout_change_requests_persistence_of_the_complete_configuration()
    {
        var viewModel = CreateViewModel();
        SnapConfiguration? requested = null;
        viewModel.SaveRequested += configuration => requested = configuration;

        viewModel.Editor!.ApplyTemplate(LayoutTemplate.ThreeColumns);

        Assert.NotNull(requested);
        Assert.Equal(2, requested.Profiles.Count);
        Assert.Equal(3, requested.Profiles[0].Monitors[0].Zones.Count);
    }

    [Fact]
    public void Setting_change_requests_persistence_immediately()
    {
        var viewModel = CreateViewModel();
        SnapConfiguration? requested = null;
        viewModel.SaveRequested += configuration => requested = configuration;

        viewModel.Settings.ZoneGap = 17;

        Assert.NotNull(requested);
        Assert.Equal(17, requested.Settings.ZoneGap);
        Assert.Equal(2, requested.Profiles.Count);
    }

    [Fact]
    public void Adding_a_profile_requests_persistence_immediately()
    {
        var viewModel = CreateViewModel();
        SnapConfiguration? requested = null;
        viewModel.SaveRequested += configuration => requested = configuration;

        viewModel.AddProfile();

        Assert.NotNull(requested);
        Assert.Equal(3, requested.Profiles.Count);
        Assert.Equal(requested.Profiles[^1].Id, requested.Settings.ActiveProfileId);
    }

    [Fact]
    public void Renaming_a_profile_requests_persistence_immediately()
    {
        var viewModel = CreateViewModel();
        SnapConfiguration? requested = null;
        viewModel.SaveRequested += configuration => requested = configuration;

        viewModel.RenameSelectedProfile("Büro");

        Assert.NotNull(requested);
        Assert.Equal("Büro", requested.Profiles[0].Name);
    }

    [Fact]
    public void Deleting_a_profile_requests_persistence_immediately()
    {
        var viewModel = CreateViewModel();
        SnapConfiguration? requested = null;
        viewModel.SaveRequested += configuration => requested = configuration;

        viewModel.DeleteSelectedProfile();

        Assert.NotNull(requested);
        Assert.Single(requested.Profiles);
        Assert.Equal(requested.Profiles[0].Id, requested.Settings.ActiveProfileId);
    }

    [Fact]
    public void Selecting_a_profile_requests_persistence_immediately()
    {
        var viewModel = CreateViewModel();
        SnapConfiguration? requested = null;
        viewModel.SaveRequested += configuration => requested = configuration;

        viewModel.SelectedProfile = viewModel.Profiles[1];

        Assert.NotNull(requested);
        Assert.Equal(viewModel.Profiles[1].Id, requested.Settings.ActiveProfileId);
    }

    [Fact]
    public void Activating_a_profile_from_a_quick_action_requests_persistence_immediately()
    {
        var viewModel = CreateViewModel();
        SnapConfiguration? requested = null;
        viewModel.SaveRequested += configuration => requested = configuration;

        viewModel.ActivateProfile(viewModel.Profiles[1].Id);

        Assert.NotNull(requested);
        Assert.Equal(viewModel.Profiles[1].Id, requested.Settings.ActiveProfileId);
    }

    [Fact]
    public void Replacing_configuration_applies_all_imported_profiles_layouts_and_settings()
    {
        var viewModel = CreateViewModel();
        var imported = ConfigurationSamples.TwoProfiles();
        imported = imported with
        {
            Settings = imported.Settings with
            {
                ActiveProfileId = imported.Profiles[1].Id,
                ZoneGap = 37,
                ThemeMode = ThemeMode.Dark
            }
        };

        viewModel.ReplaceConfiguration(imported);

        Assert.Equal(imported, viewModel.Configuration);
        Assert.Equal(37, viewModel.Settings.ZoneGap);
        Assert.Equal(ThemeMode.Dark, viewModel.Settings.ThemeMode);
        Assert.Equal(imported.Profiles[1].Id, viewModel.SelectedProfile.Id);
        Assert.Equal(imported.Profiles[1].Monitors[0].Zones, viewModel.Editor!.Zones);
    }

    [Fact]
    public void Invalid_intermediate_setting_does_not_replace_the_persisted_snapshot()
    {
        var viewModel = CreateViewModel();
        var saveRequests = 0;
        viewModel.SaveRequested += _ => saveRequests++;

        viewModel.Settings.OverlayColor = "#12";

        Assert.Equal(0, saveRequests);
        Assert.Equal("Ungültige Eingabe", viewModel.StatusMessage);
    }

    private static MainViewModel CreateViewModel()
    {
        var configuration = ConfigurationSamples.TwoProfiles();
        var monitor = configuration.Profiles[0].Monitors[0].Monitor;
        var liveMonitor = new LiveMonitor(
            monitor,
            new MonitorWorkArea(0, 0, 3440, 1440),
            96,
            96,
            true);
        return new MainViewModel(configuration, [liveMonitor]);
    }
}
