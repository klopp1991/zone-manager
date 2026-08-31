using ZoneManager.Windows.Startup;
using Xunit;

namespace ZoneManager.Tests.Profiles;

public sealed class WindowsStartupServiceTests
{
    [Fact]
    public void BuildCommand_quotes_executable_and_adds_autostart_marker()
    {
        var command = WindowsStartupService.BuildCommand(@"C:\Program Files\SnapZones\ZoneManager.exe");

        Assert.Equal("\"C:\\Program Files\\SnapZones\\ZoneManager.exe\" --autostart", command);
    }
}
