using System.Diagnostics;
using System.IO;
using SnapZones.Core.Updates;

namespace SnapZones.App.Services;

/// <summary>
/// Der Modus <c>--apply-update &lt;Programmdatei&gt;</c>: läuft aus der bereitgestellten Programmdatei,
/// legt sie an die Stelle der bisherigen und startet sie von dort.
///
/// <para>
/// Reihenfolge: Der Aufrufer hat bereits auf das Ende der alten Anwendung gewartet
/// (<c>--wait-for-pid</c>). Liegt das Ziel an einem Ort, in den nur Administratoren schreiben, startet
/// sich dieser Prozess einmal erhöht neu. Scheitert die Übernahme, wird die bisherige Version gestartet,
/// damit der Benutzer nicht ohne Programm dasteht, und der Grund erscheint als Hinweis.
/// </para>
/// </summary>
public static class UpdateApplyRunner
{
    public static readonly TimeSpan PredecessorTimeout = TimeSpan.FromSeconds(60);

    public sealed record Outcome(int ExitCode, string Message, bool Relaunched = false);

    public static Outcome Run(
        string stagingDirectory,
        string targetExecutablePath,
        IReadOnlyList<string> arguments,
        bool isAdministrator,
        Action<string, string, Exception?> log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetExecutablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(log);

        var targetDirectory = Path.GetDirectoryName(targetExecutablePath) ?? string.Empty;
        if (!isAdministrator && !CanWriteTo(targetDirectory))
        {
            if (StartupArguments.Contains(arguments, StartupArguments.ElevationAttempted))
            {
                var denied = $"Das Update lässt sich nicht nach {targetDirectory} schreiben; auch mit Administratorrechten wurde der Zugriff verweigert.";
                log("ERROR", denied, null);
                StartPrevious(targetExecutablePath, log);
                return new Outcome(1, denied);
            }

            var relaunch = new List<string>(arguments) { StartupArguments.ElevationAttempted };
            try
            {
                using var elevated = Process.Start(ElevatedSelfInvocation.BuildStartInfo(
                    Environment.ProcessPath ?? throw new InvalidOperationException("Der Programmpfad fehlt."),
                    relaunch));
                if (elevated is not null)
                {
                    log("INFO", $"Die Übernahme des Updates nach {targetDirectory} läuft mit Administratorrechten weiter.", null);
                    return new Outcome(0, "Die Übernahme läuft mit Administratorrechten weiter.", Relaunched: true);
                }
            }
            catch (Exception exception)
            {
                log("WARN", "Der erhöhte Neustart für die Übernahme des Updates ist fehlgeschlagen.", exception);
            }

            var cancelled = "Das Update wurde nicht übernommen, weil die Windows-Abfrage abgebrochen wurde. Die bisherige Version wird gestartet.";
            StartPrevious(targetExecutablePath, log);
            return new Outcome(2, cancelled);
        }

        var result = UpdateInstaller.Apply(stagingDirectory, targetExecutablePath, TimeProvider.System.GetUtcNow());
        if (result.Status != UpdateInstallStatus.Applied)
        {
            log("ERROR", $"Das Update liess sich nicht übernehmen: {result.Message}", null);
            StartPrevious(targetExecutablePath, log);
            return new Outcome(1, $"Das Update liess sich nicht übernehmen: {result.Message} Die bisherige Version wird gestartet.");
        }

        log("INFO", $"Update übernommen: {targetExecutablePath}", null);
        if (!TryStart(targetExecutablePath, log))
        {
            return new Outcome(1, "Die neue Version liegt an ihrem Platz, liess sich aber nicht starten.");
        }

        return new Outcome(0, "Die neue Version wurde übernommen und gestartet.");
    }

    /// <summary>
    /// Prüft die Schreibrechte, bevor irgendetwas angefasst wird — mit einer wirklich angelegten Datei,
    /// weil Zugriffslisten sich nicht verlässlich vorab auswerten lassen.
    /// </summary>
    public static bool CanWriteTo(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".zonemanager-write-{Guid.NewGuid():N}");
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    private static void StartPrevious(string targetExecutablePath, Action<string, string, Exception?> log)
    {
        if (File.Exists(targetExecutablePath))
        {
            _ = TryStart(targetExecutablePath, log);
        }
    }

    private static bool TryStart(string executablePath, Action<string, string, Exception?> log)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
                UseShellExecute = true
            });
            return process is not null;
        }
        catch (Exception exception)
        {
            log("ERROR", $"{executablePath} liess sich nicht starten.", exception);
            return false;
        }
    }
}
