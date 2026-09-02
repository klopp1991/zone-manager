using System.Diagnostics;
using System.IO;
using SnapZones.Core.Updates;

namespace SnapZones.App.Services;

/// <summary>
/// Sucht nach einer neueren Veröffentlichung und setzt sie auf Wunsch an die Stelle der laufenden
/// Programmdatei.
///
/// Gesucht wird nur, wenn der Benutzer es anstösst oder es in den Einstellungen ausdrücklich erlaubt
/// hat. Die Abfrage sendet nichts ausser der Anfrage selbst — keine Version, keine Rechnerkennung,
/// keine Zählung. Heruntergeladen wird ausschliesslich aus der Release-Ablage des Projekts.
/// </summary>
public sealed class UpdateCoordinator
{
    private readonly Func<string> currentVersion;
    private readonly Func<string?> executablePath;
    private readonly IReleaseFeed feed;
    private readonly UpdateInstaller installer;
    private readonly Action<string, string, Exception?> log;
    private ReleaseDescription? offered;

    public UpdateCoordinator(
        Func<string> currentVersion,
        Func<string?> executablePath,
        Action<string, string, Exception?> log,
        IReleaseFeed? feed = null,
        UpdateInstaller? installer = null)
    {
        this.currentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
        this.executablePath = executablePath ?? throw new ArgumentNullException(nameof(executablePath));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.feed = feed ?? new GitHubReleaseFeed($"{ProductInfo.InstanceKey}/{currentVersion()}");
        this.installer = installer ?? new UpdateInstaller();
    }

    /// <summary>Die zuletzt gefundene Veröffentlichung, sofern sie neuer ist als die laufende.</summary>
    public ReleaseDescription? Offered => offered;

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
    /// Installiert die zuletzt gefundene Veröffentlichung. Erfolgreich heisst: die neue Programmdatei
    /// liegt am Platz und wird beim nächsten Start verwendet. Der Neustart selbst ist Sache des
    /// Aufrufers, damit vorher noch alles gespeichert werden kann.
    /// </summary>
    public async Task<UpdateInstallResult> InstallAsync(CancellationToken cancellationToken)
    {
        if (offered is not { } release)
        {
            return new UpdateInstallResult(
                UpdateInstallStatus.Refused,
                "Es liegt keine geprüfte Veröffentlichung vor.");
        }

        if (executablePath() is not { Length: > 0 } path)
        {
            return new UpdateInstallResult(
                UpdateInstallStatus.Refused,
                "Der Pfad der laufenden Programmdatei ist nicht bekannt.");
        }

        var result = await installer.InstallAsync(path, release, cancellationToken).ConfigureAwait(false);
        if (result.Status == UpdateInstallStatus.Applied)
        {
            offered = null;
            log("INFO", $"Update auf {release.TagName} vorbereitet.", null);
        }
        else
        {
            log("WARN", $"Update auf {release.TagName} fehlgeschlagen: {result.Message}", null);
        }

        return result;
    }

    /// <summary>
    /// Startet die ersetzte Programmdatei neu. Der eigene Prozess muss danach enden, sonst laufen zwei
    /// Stände nebeneinander.
    /// </summary>
    public bool TryRestart()
    {
        if (executablePath() is not { Length: > 0 } path || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                WorkingDirectory = Path.GetDirectoryName(path) ?? AppContext.BaseDirectory,
                UseShellExecute = true
            });
            return process is not null;
        }
        catch (Exception exception)
        {
            log("ERROR", "Der Neustart nach dem Update ist fehlgeschlagen.", exception);
            return false;
        }
    }
}
