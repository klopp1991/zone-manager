using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace SnapZones.App.Services;

public enum ElevationStartupStatus
{
    /// <summary>Der Prozess besitzt bereits Administratorrechte oder braucht keine.</summary>
    Continue,

    /// <summary>Der Prozess läuft mit den vorhandenen Rechten weiter; erhöhte Fremdfenster bleiben ausgenommen.</summary>
    ContinueUnelevated,

    /// <summary>Ein erhöhter Prozess wurde gestartet; dieser Prozess beendet sich.</summary>
    Relaunched
}

public sealed record ElevationStartupResult(
    ElevationStartupStatus Status,
    string? Notice = null);

public static class ElevationStartupService
{
    public const string DiagnosticsArgument = "--diagnostics";
    public const string ElevationAttemptedArgument = "--elevation-attempted";
    private const int OperationCancelledError = 1223;

    /// <summary>
    /// Entscheidet vor dem Start der Oberfläche, ob eine Erhöhung versucht wird. Ist sie nicht möglich,
    /// wird sie abgebrochen oder schlägt sie fehl, läuft die Anwendung unerhöht weiter statt sich zu beenden.
    /// </summary>
    public static ElevationStartupResult EnsureElevation(
        string executablePath,
        IReadOnlyList<string> arguments,
        ElevationCapability capability,
        Func<ProcessStartInfo, bool> startElevated)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(startElevated);

        if (capability.IsElevated || Contains(arguments, DiagnosticsArgument))
        {
            return new ElevationStartupResult(ElevationStartupStatus.Continue);
        }

        if (Contains(arguments, ElevationAttemptedArgument))
        {
            return new ElevationStartupResult(
                ElevationStartupStatus.ContinueUnelevated,
                "Der neu gestartete Prozess besitzt weiterhin keine Administratorrechte.");
        }

        if (!capability.CanElevate)
        {
            return new ElevationStartupResult(ElevationStartupStatus.ContinueUnelevated, capability.Description);
        }

        return RequestElevation(executablePath, arguments, startElevated);
    }

    /// <summary>
    /// Startet den Prozess erhöht neu. Wird für den erneuten Versuch aus der Oberfläche ebenfalls verwendet.
    /// </summary>
    public static ElevationStartupResult RequestElevation(
        string executablePath,
        IReadOnlyList<string> arguments,
        Func<ProcessStartInfo, bool> startElevated)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(startElevated);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
            UseShellExecute = true,
            Verb = "runas"
        };
        foreach (var argument in arguments)
        {
            if (!string.Equals(argument, ElevationAttemptedArgument, StringComparison.OrdinalIgnoreCase))
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        // Der Marker verhindert einen erneuten Elevationsversuch bei einem fehlerhaften Neustart.
        startInfo.ArgumentList.Add(ElevationAttemptedArgument);

        try
        {
            return startElevated(startInfo)
                ? new ElevationStartupResult(ElevationStartupStatus.Relaunched)
                : new ElevationStartupResult(
                    ElevationStartupStatus.ContinueUnelevated,
                    "Windows hat keinen erhöhten Prozess gestartet.");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == OperationCancelledError)
        {
            return new ElevationStartupResult(
                ElevationStartupStatus.ContinueUnelevated,
                "Die Abfrage der Benutzerkontensteuerung wurde abgebrochen.");
        }
        catch (Exception exception)
        {
            return new ElevationStartupResult(
                ElevationStartupStatus.ContinueUnelevated,
                $"Der Neustart mit Administratorrechten ist fehlgeschlagen: {exception.Message}");
        }
    }

    private static bool Contains(IEnumerable<string> arguments, string expected) =>
        arguments.Contains(expected, StringComparer.OrdinalIgnoreCase);
}
