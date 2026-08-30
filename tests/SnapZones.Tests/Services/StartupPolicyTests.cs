using SnapZones.Presentation.Services;
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
}
