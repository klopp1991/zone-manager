using ZoneManager.Core.Layouts;
using ZoneManager.Core.Models;
using Xunit;

namespace ZoneManager.Tests.Layouts;

public sealed class SnapActivationPolicyTests
{
    [Fact]
    public void Active_layout_enables_snapping_independently_of_the_legacy_setting()
    {
        var configuration = ConfigurationWithLayout(isActive: true) with
        {
            Settings = AppSettings.Default(Guid.Empty) with { SnappingEnabled = false }
        };

        Assert.True(SnapActivationPolicy.ShouldEnable(configuration));
    }

    [Fact]
    public void Missing_active_layout_keeps_snapping_disabled_independently_of_the_legacy_setting()
    {
        var configuration = ConfigurationWithLayout(isActive: false) with
        {
            Settings = AppSettings.Default(Guid.Empty) with { SnappingEnabled = true }
        };

        Assert.False(SnapActivationPolicy.ShouldEnable(configuration));
    }

    private static SnapConfiguration ConfigurationWithLayout(bool isActive)
    {
        var monitor = new MonitorIdentity("DISPLAY-A", "\\\\.\\DISPLAY1", "Hauptmonitor");
        var layout = new MonitorLayout(
            monitor,
            2560,
            1440,
            [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)])
        {
            Name = "Standard",
            IsActive = isActive
        };
        return new SnapConfiguration(
            SnapConfiguration.CurrentSchemaVersion,
            AppSettings.Default(Guid.Empty),
            [layout]);
    }
}
