using SnapZones.Core.Models;
using SnapZones.Windows.Displays;
using Xunit;

namespace SnapZones.Tests.Monitors;

public sealed class DisplayPathIdentityTests
{
    [Fact]
    public void Resolve_prefers_display_path_name_and_stable_id_over_generic_pnp_fallback()
    {
        var paths = new Dictionary<string, DisplayPathIdentity>(StringComparer.OrdinalIgnoreCase)
        {
            ["\\\\.\\DISPLAY2"] = new(
                "\\\\.\\DISPLAY2",
                "\\\\?\\DISPLAY#DEL40A9#5&123",
                "DELL U3225QE")
        };

        var result = DisplayPathIdentity.Resolve(
            "\\\\.\\DISPLAY2",
            "MONITOR\\GENERIC",
            "Generic PnP Monitor",
            paths);

        Assert.Equal(new MonitorIdentity(
            "\\\\?\\DISPLAY#DEL40A9#5&123",
            "\\\\.\\DISPLAY2",
            "DELL U3225QE",
            "DEL40A9"), result);
    }

    [Fact]
    public void Resolve_keeps_existing_identity_when_display_path_is_unavailable()
    {
        var result = DisplayPathIdentity.Resolve(
            "\\\\.\\DISPLAY1",
            "MONITOR\\ABC",
            "Existing Monitor",
            new Dictionary<string, DisplayPathIdentity>());

        Assert.Equal(new MonitorIdentity("MONITOR\\ABC", "\\\\.\\DISPLAY1", "Existing Monitor"), result);
    }
}
