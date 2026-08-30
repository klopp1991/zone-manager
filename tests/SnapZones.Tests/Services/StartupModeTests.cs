using SnapZones.App.Services;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class StartupModeTests
{
    [Fact]
    public void Resolve_selects_the_dpi_probe_before_every_other_startup_mode()
    {
        var mode = StartupModeResolver.Resolve(["--diagnostics", "--dpi-probe"]);

        Assert.Equal(StartupMode.DpiProbe, mode);
    }

    [Fact]
    public void Resolve_selects_diagnostics_without_initializing_the_normal_mode()
    {
        var mode = StartupModeResolver.Resolve(["--diagnostics"]);

        Assert.Equal(StartupMode.Diagnostics, mode);
    }
}
