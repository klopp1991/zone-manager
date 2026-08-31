using System.ComponentModel;
using System.Diagnostics;
using ZoneManager.App.Services;
using Xunit;

namespace ZoneManager.Tests.Services;

public sealed class ElevationStartupServiceTests
{
    private static ElevationCapability Elevatable() => ElevationCapability.Inspect(
        isElevated: false,
        isAdministratorMember: true,
        isUserAccountControlEnabled: true,
        isInteractiveSession: true);

    private static ElevationCapability Elevated() => ElevationCapability.Inspect(
        isElevated: true,
        isAdministratorMember: true,
        isUserAccountControlEnabled: true,
        isInteractiveSession: true);

    [Fact]
    public void EnsureElevation_relaunches_a_normal_non_elevated_start_with_all_arguments()
    {
        ProcessStartInfo? capturedStartInfo = null;

        var result = ElevationStartupService.EnsureElevation(
            @"C:\Program Files\ZoneManager.exe",
            ["--autostart", "--sample", "Wert mit Leerzeichen"],
            Elevatable(),
            startInfo =>
            {
                capturedStartInfo = startInfo;
                return true;
            });

        Assert.Equal(ElevationStartupStatus.Relaunched, result.Status);
        Assert.Null(result.Notice);
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
        bool isElevated,
        string[] arguments)
    {
        var result = ElevationStartupService.EnsureElevation(
            @"C:\ZoneManager.exe",
            arguments,
            isElevated ? Elevated() : Elevatable(),
            _ => throw new InvalidOperationException("Ein Neustart wäre in diesem Fall falsch."));

        Assert.Equal(ElevationStartupStatus.Continue, result.Status);
        Assert.Null(result.Notice);
    }

    [Fact]
    public void EnsureElevation_continues_unelevated_when_the_relaunched_process_stays_unelevated()
    {
        var result = ElevationStartupService.EnsureElevation(
            @"C:\ZoneManager.exe",
            ["--elevation-attempted"],
            Elevatable(),
            _ => throw new InvalidOperationException("Eine Neustartschleife darf nicht entstehen."));

        Assert.Equal(ElevationStartupStatus.ContinueUnelevated, result.Status);
        Assert.NotNull(result.Notice);
    }

    [Fact]
    public void EnsureElevation_continues_unelevated_when_a_uac_request_is_cancelled()
    {
        var result = ElevationStartupService.EnsureElevation(
            @"C:\ZoneManager.exe",
            [],
            Elevatable(),
            _ => throw new Win32Exception(1223));

        Assert.Equal(ElevationStartupStatus.ContinueUnelevated, result.Status);
        Assert.Contains("abgebrochen", result.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureElevation_continues_unelevated_after_an_unexpected_relaunch_failure()
    {
        var result = ElevationStartupService.EnsureElevation(
            @"C:\ZoneManager.exe",
            [],
            Elevatable(),
            _ => throw new Win32Exception(5, "Zugriff verweigert"));

        Assert.Equal(ElevationStartupStatus.ContinueUnelevated, result.Status);
        Assert.Contains("Zugriff verweigert", result.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureElevation_continues_unelevated_when_windows_starts_no_elevated_process()
    {
        var result = ElevationStartupService.EnsureElevation(
            @"C:\ZoneManager.exe",
            [],
            Elevatable(),
            _ => false);

        Assert.Equal(ElevationStartupStatus.ContinueUnelevated, result.Status);
        Assert.NotNull(result.Notice);
    }

    [Fact]
    public void EnsureElevation_does_not_attempt_an_impossible_elevation()
    {
        var capability = ElevationCapability.Inspect(
            isElevated: false,
            isAdministratorMember: false,
            isUserAccountControlEnabled: true,
            isInteractiveSession: true);

        var result = ElevationStartupService.EnsureElevation(
            @"C:\ZoneManager.exe",
            [],
            capability,
            _ => throw new InvalidOperationException("Ohne Aussicht auf Erfolg darf kein Prozess starten."));

        Assert.Equal(ElevationStartupStatus.ContinueUnelevated, result.Status);
        Assert.Equal(capability.Description, result.Notice);
    }

    [Fact]
    public void RequestElevation_does_not_duplicate_the_attempt_marker()
    {
        ProcessStartInfo? capturedStartInfo = null;

        var result = ElevationStartupService.RequestElevation(
            @"C:\ZoneManager.exe",
            ["--elevation-attempted", "--autostart"],
            startInfo =>
            {
                capturedStartInfo = startInfo;
                return true;
            });

        Assert.Equal(ElevationStartupStatus.Relaunched, result.Status);
        Assert.NotNull(capturedStartInfo);
        Assert.Equal(["--autostart", "--elevation-attempted"], capturedStartInfo.ArgumentList);
    }
}
