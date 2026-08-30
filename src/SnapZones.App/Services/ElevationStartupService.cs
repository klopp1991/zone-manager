using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace SnapZones.App.Services;

public enum ElevationStartupStatus
{
    Continue,
    Relaunched,
    Cancelled,
    Failed
}

public sealed record ElevationStartupResult(
    ElevationStartupStatus Status,
    string? ErrorMessage = null);

public static class ElevationStartupService
{
    private const string DiagnosticsArgument = "--diagnostics";
    private const string ElevationAttemptedArgument = "--elevation-attempted";
    private const int OperationCancelledError = 1223;

    public static ElevationStartupResult EnsureElevation(
        string executablePath,
        IReadOnlyList<string> arguments,
        bool isAdministrator,
        Func<ProcessStartInfo, bool> startElevated)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(startElevated);

        if (isAdministrator || Contains(arguments, DiagnosticsArgument))
        {
            return new ElevationStartupResult(ElevationStartupStatus.Continue);
        }

        if (Contains(arguments, ElevationAttemptedArgument))
        {
            return new ElevationStartupResult(
                ElevationStartupStatus.Failed,
                "Der neu gestartete Prozess besitzt weiterhin keine Administratorrechte.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
            UseShellExecute = true,
            Verb = "runas"
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Der Marker verhindert einen erneuten Elevationsversuch bei einem fehlerhaften Neustart.
        startInfo.ArgumentList.Add(ElevationAttemptedArgument);

        try
        {
            return startElevated(startInfo)
                ? new ElevationStartupResult(ElevationStartupStatus.Relaunched)
                : new ElevationStartupResult(
                    ElevationStartupStatus.Failed,
                    "Windows hat keinen erhöhten Prozess gestartet.");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == OperationCancelledError)
        {
            return new ElevationStartupResult(ElevationStartupStatus.Cancelled);
        }
        catch (Exception exception)
        {
            return new ElevationStartupResult(ElevationStartupStatus.Failed, exception.Message);
        }
    }

    private static bool Contains(IEnumerable<string> arguments, string expected) =>
        arguments.Contains(expected, StringComparer.OrdinalIgnoreCase);
}
