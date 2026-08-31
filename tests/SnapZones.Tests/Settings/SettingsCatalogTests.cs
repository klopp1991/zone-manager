using SnapZones.Core.Models;
using SnapZones.Core.Settings;
using Xunit;

namespace SnapZones.Tests.Settings;

public sealed class SettingsCatalogTests
{
    [Fact]
    public void Every_setting_key_has_exactly_one_descriptor()
    {
        foreach (var key in Enum.GetValues<SettingKey>())
        {
            Assert.Single(SettingsCatalog.All, descriptor => descriptor.Key == key);
        }

        Assert.Equal(Enum.GetValues<SettingKey>().Length, SettingsCatalog.All.Count);
    }

    [Theory]
    [MemberData(nameof(AllDescriptors))]
    public void Every_setting_explains_itself(SettingDescriptor descriptor)
    {
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Label));
        Assert.False(string.IsNullOrWhiteSpace(descriptor.ShortHelp));
        Assert.False(string.IsNullOrWhiteSpace(descriptor.LongHelp));

        // The expanded help has to add information, not repeat the summary.
        Assert.NotEqual(descriptor.ShortHelp, descriptor.LongHelp);
        Assert.True(
            descriptor.LongHelp.Length > descriptor.ShortHelp.Length,
            $"The long help for '{descriptor.Key}' should be more detailed than the short help.");
    }

    [Theory]
    [MemberData(nameof(AllDescriptors))]
    public void Help_text_is_written_as_sentences(SettingDescriptor descriptor)
    {
        Assert.EndsWith(".", descriptor.ShortHelp, StringComparison.Ordinal);
        Assert.EndsWith(".", descriptor.LongHelp, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(NumericDescriptors))]
    public void Numeric_defaults_lie_inside_their_own_range(SettingDescriptor descriptor)
    {
        var range = Assert.IsType<NumericSettingRange>(descriptor.Range);

        Assert.True(range.Minimum < range.Maximum, $"'{descriptor.Key}' has an empty range.");
        Assert.InRange(range.Default, range.Minimum, range.Maximum);
        Assert.True(range.Step > 0, $"'{descriptor.Key}' needs a positive step.");
        Assert.False(string.IsNullOrWhiteSpace(range.Unit));
    }

    [Fact]
    public void Catalog_defaults_match_the_factory_settings()
    {
        var defaults = AppSettings.Default(Guid.NewGuid());

        Assert.Equal(SettingsCatalog.OuterMarginRange.Default, defaults.EffectiveOuterMargins.Left);
        Assert.Equal(SettingsCatalog.ZoneGapRange.Default, defaults.ZoneGap);
        Assert.Equal(SettingsCatalog.MagnetThresholdRange.Default, defaults.MagnetThresholdPixels);
        Assert.Equal(SettingsCatalog.OverlayOpacityRange.Default, defaults.OverlayOpacity * 100);
        Assert.Equal(SettingsCatalog.DefaultOverlayColor, defaults.OverlayColor);
    }

    [Fact]
    public void Outer_margin_range_matches_the_clamping_applied_by_the_settings_model()
    {
        var range = SettingsCatalog.OuterMarginRange;
        var beyondBoth = new AppSettings(
            Guid.Empty,
            SnappingEnabled: false,
            StartWithWindows: false,
            OverlayScope.AllMonitors,
            TriggerMode.Immediate,
            OuterMargin: 0,
            ZoneGap: 0,
            OverlayColor: SettingsCatalog.DefaultOverlayColor,
            OverlayOpacity: 0.24,
            OuterMargins: new EdgeInsets(-50, 5_000, 12, 0));

        var clamped = beyondBoth.EffectiveOuterMargins;

        Assert.Equal(range.Minimum, clamped.Left);
        Assert.Equal(range.Maximum, clamped.Top);
        Assert.Equal(12, clamped.Right);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(1000, 80)]
    [InlineData(41.6, 42)]
    public void Zone_gap_range_clamps_and_rounds(double input, int expected) =>
        Assert.Equal(expected, SettingsCatalog.ZoneGapRange.ClampToInt(input));

    [Fact]
    public void A_value_that_is_not_a_number_falls_back_to_the_default() =>
        Assert.Equal(
            SettingsCatalog.ZoneGapRange.Default,
            SettingsCatalog.ZoneGapRange.ClampToInt(double.NaN));

    [Fact]
    public void Search_finds_a_setting_by_its_label()
    {
        var results = SettingsCatalog.Search("Magnetdistanz");

        Assert.Equal(SettingKey.MagnetThreshold, Assert.Single(results).Key);
    }

    [Fact]
    public void Search_finds_a_setting_by_a_word_that_only_appears_in_the_help_text()
    {
        // "Alt" is explained in the long help of the magnet setting but is not part of its label.
        var results = SettingsCatalog.Search("Alt-Taste");

        Assert.Contains(results, descriptor => descriptor.Key == SettingKey.MagnetThreshold);
    }

    [Fact]
    public void Search_finds_a_setting_by_a_synonym_the_user_is_likely_to_type()
    {
        Assert.Contains(
            SettingsCatalog.Search("Transparenz"),
            descriptor => descriptor.Key == SettingKey.OverlayOpacity);

        Assert.Contains(
            SettingsCatalog.Search("Dark Mode"),
            descriptor => descriptor.Key == SettingKey.ThemeMode);
    }

    [Fact]
    public void Search_ignores_case_and_surrounding_whitespace()
    {
        var results = SettingsCatalog.Search("  DECKKRAFT  ");

        Assert.Contains(results, descriptor => descriptor.Key == SettingKey.OverlayOpacity);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_search_returns_every_setting(string? term) =>
        Assert.Equal(SettingsCatalog.All.Count, SettingsCatalog.Search(term).Count);

    [Fact]
    public void Search_returns_nothing_for_a_term_that_does_not_occur()
    {
        Assert.Empty(SettingsCatalog.Search("Bluetooth"));
    }

    [Fact]
    public void Every_category_has_a_label_and_a_description()
    {
        foreach (var category in Enum.GetValues<SettingCategory>())
        {
            Assert.False(string.IsNullOrWhiteSpace(SettingsCatalog.CategoryLabel(category)));
            Assert.False(string.IsNullOrWhiteSpace(SettingsCatalog.CategoryDescription(category)));
        }
    }

    [Fact]
    public void Every_category_contains_at_least_one_setting()
    {
        foreach (var category in Enum.GetValues<SettingCategory>())
        {
            Assert.NotEmpty(SettingsCatalog.InCategory(category));
        }
    }

    [Fact]
    public void Categories_partition_the_catalog()
    {
        var grouped = Enum.GetValues<SettingCategory>()
            .SelectMany(SettingsCatalog.InCategory)
            .ToArray();

        Assert.Equal(SettingsCatalog.All.Count, grouped.Length);
    }

    [Fact]
    public void Looking_up_an_unknown_key_fails_loudly() =>
        Assert.Throws<KeyNotFoundException>(() => SettingsCatalog.For((SettingKey)999));

    [Fact]
    public void Ranges_are_rendered_for_display_with_their_unit()
    {
        Assert.Equal("0 – 400 px", SettingsCatalog.OuterMarginRange.DisplayRange);
        Assert.Equal("8 px", SettingsCatalog.OuterMarginRange.DisplayDefault);
        Assert.Equal("8 – 75 %", SettingsCatalog.OverlayOpacityRange.DisplayRange);
    }

    public static TheoryData<SettingDescriptor> AllDescriptors()
    {
        var data = new TheoryData<SettingDescriptor>();
        foreach (var descriptor in SettingsCatalog.All)
        {
            data.Add(descriptor);
        }

        return data;
    }

    public static TheoryData<SettingDescriptor> NumericDescriptors()
    {
        var data = new TheoryData<SettingDescriptor>();
        foreach (var descriptor in SettingsCatalog.All.Where(descriptor => descriptor.IsNumeric))
        {
            data.Add(descriptor);
        }

        return data;
    }
}
