using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace SnapZones.App.Services;

public enum ElevatedRunStatus
{
    /// <summary>Der Hilfsprozess ist gelaufen; <see cref="ElevatedRunResult.ExitCode"/> sagt, wie.</summary>
    Completed,

    /// <summary>Die Windows-Abfrage wurde abgebrochen; nichts ist geschehen.</summary>
    Cancelled,

    /// <summary>Der Hilfsprozess hat nicht innerhalb der Zeitgrenze geendet.</summary>
    TimedOut,

    /// <summary>Der Hilfsprozess liess sich nicht starten.</summary>
    Failed
}

public sealed record ElevatedRunResult(ElevatedRunStatus Status, int ExitCode, string? Message = null)
{
    public bool Succeeded => Status == ElevatedRunStatus.Completed && ExitCode == 0;
}

/// <summary>
/// Startet die eigene Programmdatei ein zweites Mal mit Administratorrechten für eine einzelne
/// Aufgabe — Installation, Zertifikat — und wartet auf ihr Ende.
///
/// <para>
/// Das Programm läuft voreingestellt ohne Administratorrechte. Bis zum 04.09.2026 liefen die Installation
/// nach «Programme» und das Einrichten des Zertifikats trotzdem im eigenen Prozess und scheiterten dort
/// mit «Zugriff verweigert». Ein Hilfsprozess mit eigener UAC-Abfrage bekommt die Rechte genau für diese
/// eine Aufgabe; die laufende Anwendung bleibt gewöhnlich berechtigt.
/// </para>
/// </summary>
public sealed class ElevatedSelfInvocation
{
    private const int OperationCancelledError = 1223;
    private readonly string executablePath;
    private readonly Func<ProcessStartInfo, Process?> start;

    public ElevatedSelfInvocation(string executablePath, Func<ProcessStartInfo, Process?>? start = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        this.executablePath = executablePath;
        this.start = start ?? Process.Start;
    }

    public static ProcessStartInfo BuildStartInfo(string executablePath, IEnumerable<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
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

        return startInfo;
    }

    /// <summary>Läuft im Hintergrund; die Oberfläche bleibt bedienbar, während Windows nachfragt.</summary>
    public Task<ElevatedRunResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return Task.Run(() => Run(arguments, timeout));
    }

    /// <summary>Blockiert bis zum Ende des Hilfsprozesses; nicht auf dem UI-Thread aufrufen.</summary>
    public ElevatedRunResult Run(IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        Process? process;
        try
        {
            process = start(BuildStartInfo(executablePath, arguments));
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == OperationCancelledError)
        {
            return new ElevatedRunResult(
                ElevatedRunStatus.Cancelled,
                -1,
                "Die Windows-Abfrage wurde abgebrochen; es wurde nichts geändert.");
        }
        catch (Exception exception)
        {
            return new ElevatedRunResult(ElevatedRunStatus.Failed, -1, exception.Message);
        }

        if (process is null)
        {
            return new ElevatedRunResult(
                ElevatedRunStatus.Failed,
                -1,
                "Windows hat keinen erhöhten Prozess gestartet.");
        }

        using (process)
        {
            if (!process.WaitForExit((int)Math.Clamp(timeout.TotalMilliseconds, 1000, int.MaxValue)))
            {
                return new ElevatedRunResult(
                    ElevatedRunStatus.TimedOut,
                    -1,
                    "Der erhöhte Hilfsprozess hat nicht innerhalb der Zeitgrenze geantwortet.");
            }

            return new ElevatedRunResult(ElevatedRunStatus.Completed, process.ExitCode);
        }
    }
}
