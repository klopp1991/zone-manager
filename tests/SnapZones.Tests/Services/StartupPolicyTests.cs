using SnapZones.App.Services;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class StartupPolicyTests
{
    [Theory]
    [InlineData(false, true, StartupDisposition.StartVisible)]
    [InlineData(true, true, StartupDisposition.StartHidden)]
    [InlineData(false, false, StartupDisposition.ActivateRunningInstance)]
    [InlineData(true, false, StartupDisposition.ExitDuplicate)]
    public void Startup_disposition_depends_on_launch_source_and_instance_ownership(
        bool isAutoStart,
        bool isPrimary,
        StartupDisposition expected)
    {
        string[] arguments = isAutoStart ? ["--autostart"] : [];

        var disposition = StartupPolicy.Decide(arguments, isPrimary);

        Assert.Equal(expected, disposition);
    }

    [Fact]
    public void Autostart_marker_is_case_insensitive()
    {
        var disposition = StartupPolicy.Decide(["--AUTOSTART"], isPrimary: true);

        Assert.Equal(StartupDisposition.StartHidden, disposition);
    }

    [Fact]
    public void An_exit_request_asks_the_running_instance_to_stop_and_never_starts_anything()
    {
        // Ein Build tauscht die Programmdatei erst aus, wenn die laufende Instanz beendet ist; dafuer
        // bittet er sie ueber --exit. Laeuft keine, ist die Bitte erfuellt.
        Assert.Equal(StartupDisposition.StopRunningInstance, StartupPolicy.Decide(["--exit"], isPrimary: false));
        Assert.Equal(StartupDisposition.ExitDuplicate, StartupPolicy.Decide(["--EXIT"], isPrimary: true));
        Assert.Equal(StartupDisposition.StopRunningInstance, StartupPolicy.Decide(["--autostart", "--exit"], isPrimary: false));
    }
}
