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
/// Die laufende Programmdatei wird von hier aus nie angefasst. An einer Veröffentlichung hängt genau
/// eine Datei, das Installationspaket; es landet geprüft in einem Bereitstellungsverzeichnis und wird
/// von dort über <c>msiexec</c> gestartet. Windows Installer ersetzt Programmdatei und Fensterhelfer
/// gemeinsam, sobald diese Anwendung beendet ist. Siehe <see cref="UpdateInstaller"/>.
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
    private string? stagedPackagePath;

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
        // Ohne hinterlegten Zugangsschluessel bleibt beides so anonym wie bisher.
        this.feed = feed ?? new GitHubReleaseFeed(
            $"{ProductInfo.InstanceKey}/{currentVersion()}",
            accessToken: GitHubAuthService.ReadToken);
        this.installer = installer ?? new UpdateInstaller(accessToken: GitHubAuthService.ReadToken);
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
    /// Stellt die zuletzt gefundene Veröffentlichung bereit. Erfolgreich heisst: das Installationspaket
    /// liegt geprüft im Bereitstellungsverzeichnis. Übernommen wird es erst beim Beenden, siehe
    /// <see cref="TryLaunchInstaller"/>.
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

        var result = await installer.StageAsync(release, stagingDirectory(), cancellationToken).ConfigureAwait(false);
        if (result.Status == UpdateInstallStatus.Staged)
        {
            offered = null;
            IsStaged = true;
            stagedPackagePath = result.PackagePath;
            log("INFO", $"Update auf {release.TagName} bereitgestellt in {stagingDirectory()}.", null);
        }
        else
        {
            log("WARN", $"Update auf {release.TagName} fehlgeschlagen: {result.Message}", null);
        }

        return result;
    }

    /// <summary>
    /// Startet das bereitgestellte Installationspaket. Windows Installer fragt dabei einmal nach
    /// Administratorrechten und ersetzt Programmdatei und Fensterhelfer gemeinsam.
    ///
    /// <para>
    /// Der eigene Prozess muss danach enden. Windows Installer erkennt eine laufende Programmdatei über
    /// den Neustart-Manager; bis er beim Kopieren ankommt, ist diese Anwendung längst beendet. Bleibt
    /// sie stehen, legt er den gewohnten Dialog «Dateien in Benutzung» vor, statt etwas halb zu
    /// ersetzen.
    /// </para>
    /// </summary>
    public bool TryLaunchInstaller()
    {
        if (!IsStaged || stagedPackagePath is not { Length: > 0 } package)
        {
            return false;
        }

        if (!File.Exists(package))
        {
            log("ERROR", "Das bereitgestellte Installationspaket ist nicht mehr vorhanden.", null);
            IsStaged = false;
            return false;
        }

        // Ueber die Shell, weil ein MSI seine Rechte selbst anfordert; ohne sie startet msiexec
        // unerhoeht und bricht beim Schreiben nach «Programme» ab.
        var startInfo = new ProcessStartInfo
        {
            FileName = "msiexec.exe",
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add("/i");
        startInfo.ArgumentList.Add(package);
        // Fortschritt ohne Rueckfragen: der Benutzer hat das Update im Programm schon bestaetigt.
        startInfo.ArgumentList.Add("/qb");
        startInfo.ArgumentList.Add("/norestart");

        try
        {
            if (start(startInfo))
            {
                log("INFO", $"Das Installationspaket {Path.GetFileName(package)} wurde gestartet.", null);
                return true;
            }

            log("ERROR", "Windows hat das Installationspaket nicht gestartet.", null);
            return false;
        }
        catch (Exception exception)
        {
            log("ERROR", "Der Start des Installationspakets ist fehlgeschlagen.", exception);
            return false;
        }
    }

    private static bool DefaultStart(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo);
        return process is not null;
    }
}
