using SnapZones.Core.Editor;
using SnapZones.Core.Geometry;
using Xunit;

namespace SnapZones.Tests.Editor;

public sealed class LayoutSuggestionSelectorTests
{
    [Fact]
    public void Recommend_prioritizes_horizontal_splits_for_portrait_monitors()
    {
        var context = new LayoutSuggestionContext(1200, 1920, 96, 96);

        var suggestions = LayoutSuggestionSelector.Recommend(context);

        Assert.Equal(
            [LayoutTemplate.TwoRows, LayoutTemplate.MainAboveTwo, LayoutTemplate.ThreeRows],
            suggestions.Select(suggestion => suggestion.Template).Take(3));
    }

    [Fact]
    public void Recommend_limits_scaled_low_resolution_monitors_to_two_zones()
    {
        var context = new LayoutSuggestionContext(1920, 1080, 144, 144);

        var suggestions = LayoutSuggestionSelector.Recommend(context);

        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, suggestion => Assert.InRange(suggestion.Zones.Count, 1, 2));
    }

    [Fact]
    public void Recommend_offers_three_and_four_zone_layouts_for_large_widescreen_work_areas()
    {
        var context = new LayoutSuggestionContext(3840, 2160, 144, 144, 53, 30);

        var suggestions = LayoutSuggestionSelector.Recommend(context);

        Assert.Contains(suggestions, suggestion => suggestion.Zones.Count == 3);
        Assert.Contains(suggestions, suggestion => suggestion.Zones.Count == 4);
    }

    [Fact]
    public void Recommend_prioritizes_four_and_five_zone_layouts_for_super_ultrawide_monitors()
    {
        var context = new LayoutSuggestionContext(5120, 1440, 96, 96, 119, 34);

        var suggestions = LayoutSuggestionSelector.Recommend(context);

        Assert.Equal(LayoutTemplate.FourColumns, suggestions[0].Template);
        Assert.Contains(suggestions, suggestion => suggestion.Zones.Count == 5);
    }

    [Fact]
    public void Recommend_uses_physical_size_to_limit_small_high_resolution_displays()
    {
        var context = new LayoutSuggestionContext(3840, 2160, 96, 96, 30, 17);

        var suggestions = LayoutSuggestionSelector.Recommend(context);

        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, suggestion => Assert.InRange(suggestion.Zones.Count, 1, 2));
    }

    [Theory]
    [InlineData(1024, 1280, 96, 96)]
    [InlineData(1280, 1024, 96, 96)]
    [InlineData(1920, 1080, 96, 96)]
    [InlineData(3440, 1440, 96, 96)]
    [InlineData(5120, 1440, 96, 96)]
    public void Recommend_returns_only_valid_non_overlapping_templates(
        int width,
        int height,
        uint dpiX,
        uint dpiY)
    {
        var suggestions = LayoutSuggestionSelector.Recommend(
            new LayoutSuggestionContext(width, height, dpiX, dpiY));

        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, suggestion => Assert.True(ZoneGeometry.Validate(suggestion.Zones).IsValid));
    }
}
