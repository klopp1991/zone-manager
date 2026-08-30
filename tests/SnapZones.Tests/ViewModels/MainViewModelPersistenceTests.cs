using SnapZones.App.ViewModels;
using SnapZones.Core.Editor;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.ViewModels;

public sealed class MainViewModelPersistenceTests
{
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
