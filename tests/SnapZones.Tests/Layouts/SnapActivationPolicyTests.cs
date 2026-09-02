using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using Xunit;

namespace SnapZones.Tests.Layouts;

public sealed class SnapActivationPolicyTests
{
    [Fact]
    public void Active_layout_enables_snapping()
    {
        Assert.True(SnapActivationPolicy.ShouldEnable(ConfigurationWithLayout(isActive: true)));
    }

    [Fact]
    public void Missing_active_layout_keeps_snapping_disabled()
    {
        Assert.False(SnapActivationPolicy.ShouldEnable(ConfigurationWithLayout(isActive: false)));
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
