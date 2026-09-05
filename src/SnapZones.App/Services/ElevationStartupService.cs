using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using SnapZones.Core.Models;

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

    /// <summary>
    /// Entscheidet vor dem Laden der Oberfläche, ob sich das Programm erhöht.
    ///
    /// Voreingestellt geschieht das <b>nicht</b>. Ein dauerhaft erhöhter Prozess ist eine grosse
    /// Angriffsfläche: jeder ausnutzbare Fehler darin — auch in einer Abhängigkeit — wäre eine lokale
    /// Rechteausweitung. Erhöht wird deshalb nur, wenn der Benutzer es in den Einstellungen
    /// ausdrücklich verlangt hat; sonst fragt das Programm erst, wenn es tatsächlich auf ein höher
    /// berechtigtes Fenster trifft.
    ///
    /// Auch die Setup-Modi laufen ohne diesen Weg: sie holen sich die Rechte selbst, wenn sie sie
    /// brauchen, und sollen keine Abfrage auslösen, bevor klar ist, was zu tun ist.
    /// </summary>
    public static ElevationStartupResult EnsureElevation(
        string executablePath,
        IReadOnlyList<string> arguments,
        bool isAdministrator,
        ElevationMode mode,
        Func<ProcessStartInfo, bool> startElevated)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(startElevated);

        // Hilfsmodi ohne Oberflaeche brauchen keine Erhoehung beim Start: --exit bittet nur die laufende
        // Instanz um ihr Ende, und die Update-Uebernahme erhoeht sich selbst, falls das Ziel es verlangt.
        if (isAdministrator ||
            mode != ElevationMode.Always ||
            Contains(arguments, DiagnosticsArgument) ||
            Contains(arguments, StartupArguments.Exit) ||
            Contains(arguments, StartupArguments.ApplyUpdate))
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
