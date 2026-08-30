using System.Reflection;
using SnapZones.App.ViewModels;
using SnapZones.Core.Models;
using Xunit;

namespace SnapZones.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
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
