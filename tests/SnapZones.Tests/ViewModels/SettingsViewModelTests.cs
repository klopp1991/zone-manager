using SnapZones.Core.Models;
using SnapZones.Core.Settings;
using SnapZones.Presentation.ViewModels;
using Xunit;

namespace SnapZones.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    private static SettingsViewModel CreateViewModel() =>
        new(AppSettings.Default(Guid.NewGuid()));

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(40, 40)]
    [InlineData(500, 80)]
    public void Zone_gap_is_kept_in_pixels_and_clamped_to_the_catalog_range(int entered, int expected)
    {
        var viewModel = CreateViewModel();

        viewModel.ZoneGap = entered;

        Assert.Equal(expected, viewModel.ZoneGap);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(20, 20)]
    [InlineData(500, 40)]
    public void Magnet_distance_is_kept_in_pixels_and_clamped_to_the_catalog_range(int entered, int expected)
    {
        var viewModel = CreateViewModel();

        viewModel.MagnetThresholdPixels = entered;

        Assert.Equal(expected, viewModel.MagnetThresholdPixels);
    }

    [Theory]
    [InlineData(23.7, 23.5)]
    [InlineData(75.4, 75.0)]
    [InlineData(7.7, 8.0)]
    public void Overlay_opacity_input_is_clamped_and_rounded_to_half_percent(
        double enteredPercent,
        double expectedPercent)
    {
        var viewModel = CreateViewModel();

        viewModel.OverlayOpacityPercent = enteredPercent;

        Assert.Equal(expectedPercent, viewModel.OverlayOpacityPercent);
    }

    [Theory]
    [InlineData(-20, 0)]
    [InlineData(4000, 400)]
    public void Outer_margins_are_clamped_to_the_catalog_range(int entered, int expected)
    {
        var viewModel = CreateViewModel();

        viewModel.OuterMarginLeft = entered;

        Assert.Equal(expected, viewModel.OuterMarginLeft);
    }

    [Fact]
    public void Setting_the_uniform_outer_margin_updates_all_four_edges()
    {
        var viewModel = CreateViewModel();

        viewModel.OuterMargin = 24;

        Assert.Equal(24, viewModel.OuterMarginLeft);
        Assert.Equal(24, viewModel.OuterMarginTop);
        Assert.Equal(24, viewModel.OuterMarginRight);
        Assert.Equal(24, viewModel.OuterMarginBottom);
    }

    [Fact]
    public void A_freshly_loaded_default_configuration_reports_no_modified_setting()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.IsAnySettingModified);
        Assert.All(SettingsCatalog.All, descriptor => Assert.True(viewModel.Field(descriptor.Key).IsDefault));
    }

    [Fact]
    public void Changing_a_value_marks_only_that_setting_as_modified()
    {
        var viewModel = CreateViewModel();

        viewModel.ZoneGap = 40;

        Assert.True(viewModel.IsAnySettingModified);
        Assert.True(viewModel.Field(SettingKey.ZoneGap).IsModified);
        Assert.False(viewModel.Field(SettingKey.MagnetThreshold).IsModified);
    }

    [Fact]
    public void Resetting_one_setting_restores_its_default_and_leaves_the_others_alone()
    {
        var viewModel = CreateViewModel();
        viewModel.ZoneGap = 40;
        viewModel.MagnetThresholdPixels = 33;

        viewModel.ResetToDefault(SettingKey.ZoneGap);

        Assert.Equal(SettingsCatalog.ZoneGapRange.Default, viewModel.ZoneGap);
        Assert.True(viewModel.Field(SettingKey.ZoneGap).IsDefault);
        Assert.Equal(33, viewModel.MagnetThresholdPixels);
    }

    [Fact]
    public void The_reset_command_of_a_field_is_only_available_while_the_setting_is_modified()
    {
        var viewModel = CreateViewModel();
        var field = viewModel.Field(SettingKey.ZoneGap);

        Assert.False(field.ResetCommand.CanExecute(null));

        viewModel.ZoneGap = 40;
        Assert.True(field.ResetCommand.CanExecute(null));

        field.ResetCommand.Execute(null);
        Assert.Equal(SettingsCatalog.ZoneGapRange.Default, viewModel.ZoneGap);
        Assert.False(field.ResetCommand.CanExecute(null));
    }

    [Fact]
    public void Reset_all_restores_every_default()
    {
        var viewModel = CreateViewModel();
        viewModel.ZoneGap = 40;
        viewModel.MagnetThresholdPixels = 33;
        viewModel.OverlayColor = "#123456";
        viewModel.OverlayOpacityPercent = 70;
        viewModel.OuterMargin = 120;
        viewModel.ThemeMode = ThemeMode.Dark;
        viewModel.TriggerMode = TriggerMode.ShiftKey;
        viewModel.OverlayScope = OverlayScope.ActiveMonitor;
        viewModel.ShowZoneNames = false;
        viewModel.StartWithWindows = true;

        viewModel.ResetAll();

        Assert.False(viewModel.IsAnySettingModified);
        Assert.Equal(AppSettings.Default(Guid.Empty), viewModel.CreateSettings());
    }

    [Fact]
    public void Reset_all_reports_a_single_value_change_rather_than_one_per_setting()
    {
        var viewModel = CreateViewModel();
        viewModel.ZoneGap = 40;
        viewModel.MagnetThresholdPixels = 33;
        viewModel.OverlayColor = "#123456";

        var notifications = 0;
        viewModel.ValueChanged += (_, _) => notifications++;

        viewModel.ResetAll();

        Assert.Equal(1, notifications);
    }

    [Fact]
    public void Applying_a_stored_configuration_reports_a_single_value_change()
    {
        var viewModel = CreateViewModel();
        var notifications = 0;
        viewModel.ValueChanged += (_, _) => notifications++;

        viewModel.Apply(AppSettings.Default(Guid.NewGuid()) with
        {
            ZoneGap = 21,
            MagnetThresholdPixels = 4,
            OverlayColor = "#101010"
        });

        Assert.Equal(1, notifications);
        Assert.Equal(21, viewModel.ZoneGap);
        Assert.Equal(4, viewModel.MagnetThresholdPixels);
    }

    [Fact]
    public void Searching_or_unfolding_help_never_reports_a_value_change()
    {
        var viewModel = CreateViewModel();
        var notifications = 0;
        viewModel.ValueChanged += (_, _) => notifications++;

        viewModel.SearchTerm = "Deckkraft";
        viewModel.Field(SettingKey.OverlayOpacity).IsHelpExpanded = true;
        viewModel.Field(SettingKey.ZoneGap).ToggleHelpCommand.Execute(null);

        Assert.Equal(0, notifications);
    }

    [Fact]
    public void Changing_one_value_reports_exactly_one_value_change()
    {
        var viewModel = CreateViewModel();
        var notifications = 0;
        viewModel.ValueChanged += (_, _) => notifications++;

        viewModel.ZoneGap = 40;

        Assert.Equal(1, notifications);
    }

    [Fact]
    public void Assigning_the_same_value_again_reports_nothing()
    {
        var viewModel = CreateViewModel();
        viewModel.ZoneGap = 40;

        var notifications = 0;
        viewModel.ValueChanged += (_, _) => notifications++;
        viewModel.ZoneGap = 40;

        Assert.Equal(0, notifications);
    }

    [Fact]
    public void Searching_hides_the_settings_that_do_not_match()
    {
        var viewModel = CreateViewModel();

        viewModel.SearchTerm = "Magnetdistanz";

        Assert.True(viewModel.Field(SettingKey.MagnetThreshold).IsVisible);
        Assert.False(viewModel.Field(SettingKey.ZoneGap).IsVisible);
        Assert.True(viewModel.HasSearchResults);
    }

    [Fact]
    public void A_section_hides_itself_when_none_of_its_settings_match()
    {
        var viewModel = CreateViewModel();

        viewModel.SearchTerm = "Magnetdistanz";

        var program = viewModel.Sections.Single(section => section.Category == SettingCategory.Program);
        var spacing = viewModel.Sections.Single(section => section.Category == SettingCategory.Spacing);
        Assert.False(program.IsVisible);
        Assert.True(spacing.IsVisible);
    }

    [Fact]
    public void Clearing_the_search_shows_every_setting_again()
    {
        var viewModel = CreateViewModel();
        viewModel.SearchTerm = "Magnetdistanz";

        viewModel.ClearSearchCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.SearchTerm);
        Assert.All(SettingsCatalog.All, descriptor => Assert.True(viewModel.Field(descriptor.Key).IsVisible));
        Assert.All(viewModel.Sections, section => Assert.True(section.IsVisible));
    }

    [Fact]
    public void A_search_without_matches_is_reported_to_the_user()
    {
        var viewModel = CreateViewModel();

        viewModel.SearchTerm = "Bluetooth";

        Assert.False(viewModel.HasSearchResults);
        Assert.Contains("Bluetooth", viewModel.SearchResultSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void The_search_summary_is_empty_while_no_search_is_active()
    {
        Assert.Equal(string.Empty, CreateViewModel().SearchResultSummary);
    }

    [Fact]
    public void Every_setting_is_reachable_through_a_section()
    {
        var viewModel = CreateViewModel();

        var keysInSections = viewModel.Sections
            .SelectMany(section => section.Fields)
            .Select(field => field.Key)
            .ToArray();

        Assert.Equal(SettingsCatalog.All.Count, keysInSections.Length);
        Assert.Equal(SettingsCatalog.All.Count, keysInSections.Distinct().Count());
    }

    [Fact]
    public void A_round_trip_through_the_settings_model_preserves_every_value()
    {
        var viewModel = CreateViewModel();
        viewModel.ZoneGap = 17;
        viewModel.MagnetThresholdPixels = 3;
        viewModel.OverlayOpacityPercent = 41.5;
        viewModel.OverlayColor = "#ABCDEF";
        viewModel.OuterMarginLeft = 11;
        viewModel.OuterMarginTop = 12;
        viewModel.OuterMarginRight = 13;
        viewModel.OuterMarginBottom = 14;
        viewModel.ThemeMode = ThemeMode.Dark;
        viewModel.TriggerMode = TriggerMode.ShiftKey;
        viewModel.OverlayScope = OverlayScope.ActiveMonitor;
        viewModel.ShowZoneNames = false;
        viewModel.StartWithWindows = true;

        var restored = new SettingsViewModel(viewModel.CreateSettings());

        Assert.Equal(viewModel.CreateSettings(), restored.CreateSettings());
        Assert.Equal(41.5, restored.OverlayOpacityPercent);
        Assert.Equal(new EdgeInsets(11, 12, 13, 14), restored.CreateSettings().EffectiveOuterMargins);
    }
}
