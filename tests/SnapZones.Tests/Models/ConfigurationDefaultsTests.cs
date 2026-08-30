using SnapZones.Core.Models;
using Xunit;

namespace SnapZones.Tests.Models;

public sealed class ConfigurationDefaultsTests
{
    [Fact]
    public void CreateDefault_builds_safe_standard_profile()
    {
        var configuration = SnapConfiguration.CreateDefault();

        Assert.Single(configuration.Profiles);
        Assert.Equal(configuration.Profiles[0].Id, configuration.Settings.ActiveProfileId);
        Assert.Equal("Standard", configuration.Profiles[0].Name);
        Assert.False(configuration.Settings.SnappingEnabled);
        Assert.False(configuration.Settings.StartWithWindows);
        Assert.Equal(0.0, NormalizedRect.Full.X);
        Assert.Equal(1.0, NormalizedRect.Full.Width);
    }
}
