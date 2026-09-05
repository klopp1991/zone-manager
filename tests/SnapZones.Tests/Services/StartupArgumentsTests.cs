using SnapZones.App.Services;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class StartupArgumentsTests
{
    [Fact]
    public void The_value_behind_a_switch_is_read_regardless_of_case()
    {
        string[] arguments = ["--verbose", "--APPLY-UPDATE", @"C:\Programme\ZoneManager\ZoneManager.exe"];

        Assert.Equal(@"C:\Programme\ZoneManager\ZoneManager.exe", StartupArguments.ReadValue(arguments, StartupArguments.ApplyUpdate));
        Assert.Null(StartupArguments.ReadValue(["--apply-update"], StartupArguments.ApplyUpdate));
    }

    [Theory]
    [InlineData(new[] { "--wait-for-pid", "4711" }, 4711)]
    [InlineData(new[] { "--autostart", "--Wait-For-Pid", "12" }, 12)]
    [InlineData(new[] { "--wait-for-pid", "0" }, 0)]
    [InlineData(new[] { "--wait-for-pid", "-5" }, 0)]
    [InlineData(new[] { "--wait-for-pid", "abc" }, 0)]
    [InlineData(new[] { "--wait-for-pid" }, 0)]
    [InlineData(new string[0], 0)]
    public void Only_a_positive_process_id_counts_as_a_predecessor(string[] arguments, int expected)
    {
        var found = StartupArguments.TryReadWaitForPid(arguments, out var processId);

        Assert.Equal(expected > 0, found);
        Assert.Equal(expected, processId);
    }

    [Fact]
    public void A_successor_inherits_the_switches_but_not_the_one_time_markers()
    {
        string[] own = ["--verbose", "--autostart", "--elevation-attempted", "--wait-for-pid", "99"];

        var successor = StartupArguments.ForSuccessor(own, ownProcessId: 123, hidden: false);

        Assert.Equal(["--verbose", "--wait-for-pid", "123"], successor);
    }

    [Fact]
    public void A_hidden_successor_starts_in_the_tray()
    {
        var successor = StartupArguments.ForSuccessor([], ownProcessId: 7, hidden: true);

        Assert.Equal(["--autostart", "--wait-for-pid", "7"], successor);
    }
}
