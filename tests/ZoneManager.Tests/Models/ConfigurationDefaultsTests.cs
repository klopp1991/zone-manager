using ZoneManager.Core.Models;
using Xunit;

namespace ZoneManager.Tests.Models;

public sealed class ConfigurationDefaultsTests
{
    [Fact]
    public void CreateDefault_builds_a_safe_empty_layout_catalog()
    {
        var configuration = SnapConfiguration.CreateDefault();

        Assert.Equal(SnapConfiguration.CurrentSchemaVersion, configuration.SchemaVersion);
        Assert.Empty(configuration.Layouts);
        Assert.Empty(configuration.MonitorOrder);
        Assert.False(configuration.Settings.SnappingEnabled);
        Assert.False(configuration.Settings.StartWithWindows);
        Assert.Equal(ThemeMode.System, configuration.Settings.ThemeMode);
        Assert.Equal(20, configuration.Settings.MagnetThresholdPixels);
        Assert.True(configuration.Settings.ShowZoneNames);
        Assert.Equal("#707070", configuration.Settings.OverlayColor);
        Assert.Equal(EdgeInsets.Uniform(8), configuration.Settings.EffectiveOuterMargins);
        Assert.Equal(0, configuration.Settings.ZoneGap);
        Assert.Equal(0.0, NormalizedRect.Full.X);
        Assert.Equal(1.0, NormalizedRect.Full.Width);
    }
}
