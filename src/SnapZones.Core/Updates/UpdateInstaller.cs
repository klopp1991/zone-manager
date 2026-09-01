using System.Globalization;
using System.Net.Http;

namespace SnapZones.Core.Updates;

public enum UpdateInstallStatus
{
    Applied,
    Refused,
    DownloadFailed,
    ReplaceFailed
}

public sealed record UpdateInstallResult(UpdateInstallStatus Status, string Message);

/// <summary>
/// Lädt eine Veröffentlichung herunter und setzt sie an die Stelle der laufenden Programmdatei.
///
/// Windows lässt eine laufende Programmdatei nicht überschreiben, wohl aber umbenennen. Der Austausch
/// geht deshalb in drei Schritten: die neue Datei landet zuerst daneben, dann wird die laufende
/// beiseitegeschoben, dann die neue an ihren Platz gelegt. Bricht ein Schritt ab, wird der vorherige
/// Zustand wiederhergestellt — es darf nie eine halb ersetzte Programmdatei zurückbleiben.
///
/// Die beiseitegeschobene Datei bleibt liegen, bis der laufende Prozess endet; erst danach lässt sie
/// sich löschen. <see cref="RemoveSupersededFiles"/> räumt sie beim nächsten Start weg.
/// </summary>
public sealed class UpdateInstaller
{
    private const string SupersededMarker = ".previous.";
    private readonly Func<HttpClient> clientFactory;

    public UpdateInstaller(Func<HttpClient>? clientFactory = null)
    {
        this.clientFactory = clientFactory ?? (() => new HttpClient { Timeout = TimeSpan.FromMinutes(10) });
    }

    public async Task<UpdateInstallResult> InstallAsync(
        string executablePath,
        ReleaseDescription release,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(release);

        if (!UpdateCheck.IsAcceptableDownload(release, out var rejection))
        {
            return new UpdateInstallResult(UpdateInstallStatus.Refused, rejection);
        }

        var downloadPath = executablePath + ".download";
        try
        {
            await DownloadAsync(release, downloadPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            TryDelete(downloadPath);
            return new UpdateInstallResult(
                UpdateInstallStatus.DownloadFailed,
                $"Der Download ist fehlgeschlagen: {exception.Message}");
        }

        var actualSize = new FileInfo(downloadPath).Length;
        if (actualSize != release.SizeInBytes)
        {
            // Eine abgebrochene Uebertragung sieht wie eine vollstaendige Datei aus. Die angekuendigte
            // Groesse ist der einzige Pruefwert, den die Release-Ablage ohne Signatur mitliefert.
            TryDelete(downloadPath);
            return new UpdateInstallResult(
                UpdateInstallStatus.DownloadFailed,
                $"Die geladene Datei ist {actualSize} statt {release.SizeInBytes} Bytes gross und wird verworfen.");
        }

        return Replace(executablePath, downloadPath, TimeProvider.System.GetUtcNow());
    }

    /// <summary>
    /// Legt die geladene Datei an die Stelle der laufenden. Der Zeitpunkt wird hereingereicht, damit der
    /// Name der beiseitegeschobenen Datei prüfbar bleibt.
    /// </summary>
    public static UpdateInstallResult Replace(
        string executablePath,
        string downloadPath,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadPath);

        if (!File.Exists(downloadPath))
        {
            return new UpdateInstallResult(
                UpdateInstallStatus.DownloadFailed,
                "Die geladene Datei ist nicht mehr vorhanden.");
        }

        var supersededPath = BuildSupersededPath(executablePath, now);
        var moved = false;
        try
        {
            if (File.Exists(executablePath))
            {
                File.Move(executablePath, supersededPath);
                moved = true;
            }

            File.Move(downloadPath, executablePath);
            return new UpdateInstallResult(
                UpdateInstallStatus.Applied,
                "Die neue Version liegt bereit und wird beim Neustart verwendet.");
        }
        catch (Exception exception)
        {
            if (moved && !File.Exists(executablePath))
            {
                // Der zweite Schritt ist gescheitert: die alte Datei muss zurueck an ihren Platz, sonst
                // bleibt gar kein lauffaehiges Programm uebrig.
                TryMoveBack(supersededPath, executablePath);
            }

            TryDelete(downloadPath);
            return new UpdateInstallResult(
                UpdateInstallStatus.ReplaceFailed,
                $"Die Programmdatei liess sich nicht ersetzen: {exception.Message}");
        }
    }

    /// <summary>
    /// Löscht beiseitegeschobene Vorgängerdateien. Wird beim Start aufgerufen, wenn der Prozess, der sie
    /// belegte, nicht mehr läuft. Was sich nicht löschen lässt, bleibt liegen und stört nicht.
    /// </summary>
    public static int RemoveSupersededFiles(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        var directory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return 0;
        }

        var prefix = Path.GetFileName(executablePath) + SupersededMarker;
        var removed = 0;
        foreach (var candidate in Directory.EnumerateFiles(directory, prefix + "*"))
        {
            if (TryDelete(candidate))
            {
                removed++;
            }
        }

        return removed;
    }

    public static string BuildSupersededPath(string executablePath, DateTimeOffset now) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{executablePath}{SupersededMarker}{now.ToUnixTimeMilliseconds()}");

    private async Task DownloadAsync(
        ReleaseDescription release,
        string downloadPath,
        CancellationToken cancellationToken)
    {
        using var client = clientFactory();
        using var response = await client
            .GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = new FileStream(
            downloadPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous);
        await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        await target.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        return false;
    }

    private static void TryMoveBack(string from, string to)
    {
        try
        {
            File.Move(from, to);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
