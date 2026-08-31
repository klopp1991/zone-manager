using System.ComponentModel;
using System.Diagnostics;
using SnapZones.Presentation.Services;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class ElevationStartupServiceTests
{
    [WindowsOnlyFact]
    public void EnsureElevation_relaunches_a_normal_non_elevated_start_with_all_arguments()
    {
        ProcessStartInfo? capturedStartInfo = null;

        var result = ElevationStartupService.EnsureElevation(
            @"C:\Program Files\ZoneManager.exe",
            ["--autostart", "--sample", "Wert mit Leerzeichen"],
            isAdministrator: false,
            startElevated: startInfo =>
            {
                capturedStartInfo = startInfo;
                return true;
            });

        Assert.Equal(ElevationStartupStatus.Relaunched, result.Status);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(capturedStartInfo);
        Assert.Equal(@"C:\Program Files\ZoneManager.exe", capturedStartInfo.FileName);
        Assert.Equal("runas", capturedStartInfo.Verb);
        Assert.True(capturedStartInfo.UseShellExecute);
        Assert.Equal(@"C:\Program Files", capturedStartInfo.WorkingDirectory);
        Assert.Equal(
            ["--autostart", "--sample", "Wert mit Leerzeichen", "--elevation-attempted"],
            capturedStartInfo.ArgumentList);
    }

    [Theory]
    [InlineData(true, new string[0])]
    [InlineData(false, new[] { "--diagnostics" })]
    [InlineData(false, new[] { "--DIAGNOSTICS" })]
    public void EnsureElevation_continues_when_elevated_or_running_diagnostics(
        bool isAdministrator,
        string[] arguments)
    {
        var result = ElevationStartupService.EnsureElevation(
            @"C:\ZoneManager.exe",
            arguments,
            isAdministrator,
            _ => throw new InvalidOperationException("Ein Neustart wäre in diesem Fall falsch."));

        Assert.Equal(ElevationStartupStatus.Continue, result.Status);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void EnsureElevation_stops_instead_of_restarting_when_elevation_marker_remains_unelevated()
    {
        var result = ElevationStartupService.EnsureElevation(
            @"C:\ZoneManager.exe",
            ["--elevation-attempted"],
            isAdministrator: false,
            _ => throw new InvalidOperationException("Eine Neustartschleife darf nicht entstehen."));

        Assert.Equal(ElevationStartupStatus.Failed, result.Status);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void EnsureElevation_reports_a_cancelled_uac_request_without_continuing()
    {
        var result = ElevationStartupService.EnsureElevation(
            @"C:\ZoneManager.exe",
            [],
            isAdministrator: false,
            _ => throw new Win32Exception(1223));

        Assert.Equal(ElevationStartupStatus.Cancelled, result.Status);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void EnsureElevation_reports_an_unexpected_relaunch_failure()
    {
        var result = ElevationStartupService.EnsureElevation(
            @"C:\ZoneManager.exe",
            [],
            isAdministrator: false,
            _ => throw new Win32Exception(5, "Zugriff verweigert"));

        Assert.Equal(ElevationStartupStatus.Failed, result.Status);
        Assert.Contains("Zugriff verweigert", result.ErrorMessage, StringComparison.Ordinal);
    }
}
