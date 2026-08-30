using SnapZones.App.Services;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class StartupModeTests
{
    [Fact]
    public void Dpi_probe_lifetime_schedules_one_shutdown_and_ignores_duplicate_ticks()
    {
        TimeSpan? scheduledDelay = null;
        Action? scheduledCallback = null;
        var shutdownCount = 0;
        var lifetime = new DpiProbeLifetime(
            TimeSpan.FromSeconds(1),
            (delay, callback) =>
            {
                scheduledDelay = delay;
                scheduledCallback = callback;
            },
            () => shutdownCount++);

        lifetime.Start();
        scheduledCallback!.Invoke();
        scheduledCallback.Invoke();

        Assert.Equal(TimeSpan.FromSeconds(1), scheduledDelay);
        Assert.Equal(1, shutdownCount);
    }

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
