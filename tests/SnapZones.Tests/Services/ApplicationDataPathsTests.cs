using SnapZones.App.Services;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class ApplicationDataPathsTests
{
    [Fact]
    public void Resolve_uses_Data_and_Logs_next_to_executable_when_portable_flag_exists()
    {
        using var directory = new TemporaryDirectory();
        var executable = Path.Combine(directory.Path, "SnapZones.App.exe");
        File.WriteAllText(Path.Combine(directory.Path, "portable.flag"), string.Empty);

        var paths = ApplicationDataPaths.Resolve(executable, "R:\\Roaming", "L:\\Local");

        Assert.Equal(Path.Combine(directory.Path, "Data"), paths.ConfigurationDirectory);
        Assert.Equal(Path.Combine(directory.Path, "Logs"), paths.LogDirectory);
    }

    [Fact]
    public void Resolve_uses_roaming_configuration_and_local_logs_without_portable_flag()
    {
        using var directory = new TemporaryDirectory();
        var executable = Path.Combine(directory.Path, "SnapZones.App.exe");

        var paths = ApplicationDataPaths.Resolve(executable, "R:\\Roaming", "L:\\Local");

        Assert.Equal(Path.Combine("R:\\Roaming", "SnapZones"), paths.ConfigurationDirectory);
        Assert.Equal(Path.Combine("L:\\Local", "SnapZones", "logs"), paths.LogDirectory);
    }
}
