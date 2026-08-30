using System.Reflection;
using SnapZones.App.ViewModels;
using SnapZones.Core.Models;
using SnapZones.Core.Placement;
using Xunit;

namespace SnapZones.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void Default_settings_enable_window_placement_with_an_empty_rule_copy()
    {
        var viewModel = new SettingsViewModel(AppSettings.Default(Guid.NewGuid()));

        Assert.True(viewModel.RestoreWindowPlacementEnabled);
        Assert.Empty(viewModel.WindowPlacementRules);
    }

    [Fact]
    public void Placement_switch_and_rules_round_trip_without_loss()
    {
        var profileId = Guid.NewGuid();
        var rule = new WindowPlacementRule(
            Guid.NewGuid(),
            true,
            "app.exe",
            "WindowClass",
            WindowKind.MainWindow,
            null,
            WindowPlacementMode.Exclude,
            null,
            null,
            null);
        var viewModel = new SettingsViewModel(AppSettings.Default(profileId) with
        {
            RestoreWindowPlacementEnabled = false,
            WindowPlacementRules = [rule]
        });

        var saved = viewModel.CreateSettings(profileId);

        Assert.False(viewModel.RestoreWindowPlacementEnabled);
        Assert.False(saved.RestoreWindowPlacementEnabled);
        Assert.Equal([rule], saved.EffectiveWindowPlacementRules);
    }

    [Fact]
    public void Applying_settings_replaces_the_complete_rule_copy_without_aliasing_the_source_list()
    {
        var profileId = Guid.NewGuid();
        var source = new List<WindowPlacementRule>
        {
            new(Guid.NewGuid(), true, "app.exe", "Main", WindowKind.MainWindow, "Title*",
                WindowPlacementMode.FixedZone, Guid.NewGuid(), "DISPLAY-1", Guid.NewGuid())
        };
        var viewModel = new SettingsViewModel(AppSettings.Default(profileId));

        viewModel.Apply(AppSettings.Default(profileId) with { WindowPlacementRules = source });
        source.Clear();

        Assert.Single(viewModel.WindowPlacementRules);
        Assert.Equal(viewModel.WindowPlacementRules, viewModel.CreateSettings(profileId).EffectiveWindowPlacementRules);
    }

    [Theory]
    [InlineData("ZoneGapPercent", 50.5, 40)]
    [InlineData("MagnetThresholdPercent", 50.5, 20)]
    public void Percentage_input_maps_to_the_native_slider_value(
        string propertyName,
        double enteredPercent,
        int expectedNativeValue)
    {
        var viewModel = new SettingsViewModel(AppSettings.Default(Guid.NewGuid()));
        var property = typeof(SettingsViewModel).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        property.SetValue(viewModel, enteredPercent);

        Assert.Equal(enteredPercent, Assert.IsType<double>(property.GetValue(viewModel)));
        var nativeValue = propertyName == "ZoneGapPercent"
            ? viewModel.ZoneGap
            : viewModel.MagnetThresholdPixels;
        Assert.Equal(expectedNativeValue, nativeValue);
    }

    [Theory]
    [InlineData(23.7, 23.5)]
    [InlineData(75.4, 75.0)]
    [InlineData(7.7, 8.0)]
    public void Overlay_opacity_input_is_clamped_and_rounded_to_half_percent(
        double enteredPercent,
        double expectedPercent)
    {
        var viewModel = new SettingsViewModel(AppSettings.Default(Guid.NewGuid()));

        viewModel.OverlayOpacityPercent = enteredPercent;

        Assert.Equal(expectedPercent, viewModel.OverlayOpacityPercent);
    }
}
