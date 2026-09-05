using System.Diagnostics;
using System.Globalization;
using System.IO;
using SnapZones.Core.Updates;

namespace SnapZones.App.Services;

/// <summary>
/// Sucht nach einer neueren Veröffentlichung, stellt sie bereit und lässt sie nach dem Beenden übernehmen.
///
/// <para>
/// Gesucht wird nur, wenn der Benutzer es anstösst oder es in den Einstellungen ausdrücklich erlaubt
/// hat. Die Abfrage sendet nichts ausser der Anfrage selbst — keine Version, keine Rechnerkennung,
/// keine Zählung. Heruntergeladen wird ausschliesslich aus der Release-Ablage des Projekts.
/// </para>
///
/// <para>
/// Die laufende Programmdatei wird von hier aus nie angefasst. Die neue Version landet in einem
/// Bereitstellungsverzeichnis und wird von dort als eigener Prozess gestartet, der auf das Ende dieses
/// Prozesses wartet und erst dann die Dateien austauscht. Siehe <see cref="UpdateInstaller"/>.
/// </para>
/// </summary>
public sealed class UpdateCoordinator
{
    private readonly Func<string> currentVersion;
    private readonly Func<string?> executablePath;
    private readonly Func<string> stagingDirectory;
    private readonly IReleaseFeed feed;
    private readonly UpdateInstaller installer;
    private readonly Action<string, string, Exception?> log;
    private readonly Func<ProcessStartInfo, bool> start;
    private ReleaseDescription? offered;

    public UpdateCoordinator(
        Func<string> currentVersion,
        Func<string?> executablePath,
        Func<string> stagingDirectory,
        Action<string, string, Exception?> log,
        IReleaseFeed? feed = null,
        UpdateInstaller? installer = null,
        Func<ProcessStartInfo, bool>? start = null)
    {
        this.currentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
        this.executablePath = executablePath ?? throw new ArgumentNullException(nameof(executablePath));
        this.stagingDirectory = stagingDirectory ?? throw new ArgumentNullException(nameof(stagingDirectory));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.feed = feed ?? new GitHubReleaseFeed($"{ProductInfo.InstanceKey}/{currentVersion()}");
        this.installer = installer ?? new UpdateInstaller();
        this.start = start ?? DefaultStart;
    }

    /// <summary>Die zuletzt gefundene Veröffentlichung, sofern sie neuer ist als die laufende.</summary>
    public ReleaseDescription? Offered => offered;

    /// <summary>Ob eine geprüfte neue Version bereitliegt und nur noch übernommen werden muss.</summary>
    public bool IsStaged { get; private set; }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        ReleaseDescription? release;
        try
        {
            release = await feed.ReadLatestAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            log("WARN", "Die Suche nach Updates ist fehlgeschlagen.", exception);
            offered = null;
            return new UpdateCheckResult(
                UpdateAvailability.Unknown,
                null,
                null,
                $"Die Suche ist fehlgeschlagen: {exception.Message}");
        }

        var result = UpdateCheck.Evaluate(currentVersion(), release);
        offered = result.Availability == UpdateAvailability.UpdateAvailable ? result.Release : null;
        return result;
    }

    /// <summary>
    /// Stellt die zuletzt gefundene Veröffentlichung bereit. Erfolgreich heisst: Programmdatei und
    /// Fensterhelfer liegen geprüft im Bereitstellungsverzeichnis. Übernommen wird erst nach dem Beenden,
    /// siehe <see cref="TryLaunchApply"/>.
    /// </summary>
    public async Task<UpdateInstallResult> StageAsync(CancellationToken cancellationToken)
    {
        if (offered is not { } release)
        {
            return new UpdateInstallResult(
                UpdateInstallStatus.Refused,
                "Es liegt keine geprüfte Veröffentlichung vor.");
        }

        if (executablePath() is not { Length: > 0 })
        {
            return new UpdateInstallResult(
                UpdateInstallStatus.Refused,
                "Der Pfad der laufenden Programmdatei ist nicht bekannt.");
        }

        var result = await installer.StageAsync(stagingDirectory(), release, cancellationToken).ConfigureAwait(false);
        if (result.Status == UpdateInstallStatus.Staged)
        {
            offered = null;
            IsStaged = true;
            log("INFO", $"Update auf {release.TagName} bereitgestellt in {stagingDirectory()}.", null);
        }
        else
        {
            log("WARN", $"Update auf {release.TagName} fehlgeschlagen: {result.Message}", null);
        }

        return result;
    }

    /// <summary>
    /// Startet die bereitgestellte Programmdatei im Übernahmemodus. Sie wartet auf das Ende dieses
    /// Prozesses, tauscht dann die Dateien und startet die neue Version. Der eigene Prozess muss danach
    /// enden, sonst wartet der Nachfolger vergeblich.
    /// </summary>
    public bool TryLaunchApply()
    {
        if (!IsStaged || executablePath() is not { Length: > 0 } target)
        {
            return false;
        }

        var staged = UpdateInstaller.BuildStagedExecutablePath(stagingDirectory());
        if (!File.Exists(staged))
        {
            log("ERROR", "Die bereitgestellte Programmdatei ist nicht mehr vorhanden.", null);
            IsStaged = false;
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = staged,
            WorkingDirectory = Path.GetDirectoryName(target) ?? AppContext.BaseDirectory,
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add(StartupArguments.ApplyUpdate);
        startInfo.ArgumentList.Add(target);
        startInfo.ArgumentList.Add(StartupArguments.WaitForPid);
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));

        try
        {
            if (start(startInfo))
            {
                return true;
            }

            log("ERROR", "Windows hat die Übernahme des Updates nicht gestartet.", null);
            return false;
        }
        catch (Exception exception)
        {
            log("ERROR", "Der Start der Übernahme nach dem Update ist fehlgeschlagen.", exception);
            return false;
        }
    }

    private static bool DefaultStart(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo);
        return process is not null;
    }
}
