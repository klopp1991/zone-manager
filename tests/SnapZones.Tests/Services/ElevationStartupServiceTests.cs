using System.ComponentModel;
using System.Diagnostics;
using SnapZones.App.Services;
using SnapZones.Core.Models;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class ElevationStartupServiceTests
{
    [Fact]
    public void EnsureElevation_relaunches_a_normal_non_elevated_start_with_all_arguments()
    {
        ProcessStartInfo? capturedStartInfo = null;

        var result = ElevationStartupService.EnsureElevation(
            @"C:\Program Files\ZoneManager.exe",
            ["--autostart", "--sample", "Wert mit Leerzeichen"],
            isAdministrator: false,
            ElevationMode.Always,
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
            ElevationMode.Always,
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
            ElevationMode.Always,
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
            ElevationMode.Always,
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
            ElevationMode.Always,
            _ => throw new Win32Exception(5, "Zugriff verweigert"));

        Assert.Equal(ElevationStartupStatus.Failed, result.Status);
        Assert.Contains("Zugriff verweigert", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void The_default_never_elevates_at_startup()
    {
        // Ein dauerhaft erhoehter Prozess ist eine grosse Angriffsflaeche. Voreingestellt startet das
        // Programm deshalb mit gewoehnlichen Rechten und fragt erst nach, wenn es sie wirklich braucht.
        var result = ElevationStartupService.EnsureElevation(
            @"C:\ZoneManager.exe",
            [],
            isAdministrator: false,
            ElevationMode.WhenNeeded,
            _ => throw new InvalidOperationException("Voreingestellt darf nicht erhoeht werden."));

        Assert.Equal(ElevationStartupStatus.Continue, result.Status);
    }

    [Fact]
    public void Diagnostics_never_elevate_even_when_the_setting_demands_it()
    {
        var result = ElevationStartupService.EnsureElevation(
            @"C:\ZoneManager.exe",
            ["--diagnostics"],
            isAdministrator: false,
            ElevationMode.Always,
            _ => throw new InvalidOperationException("Die Diagnose laeuft bewusst ohne Elevation."));

        Assert.Equal(ElevationStartupStatus.Continue, result.Status);
    }
}
