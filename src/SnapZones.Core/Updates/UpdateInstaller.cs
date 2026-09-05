using System.Globalization;
using System.Net.Http;
using SnapZones.Core.Setup;

namespace SnapZones.Core.Updates;

public enum UpdateInstallStatus
{
    /// <summary>Die neue Version liegt vollständig geprüft im Bereitstellungsverzeichnis.</summary>
    Staged,

    /// <summary>Die neue Version liegt an der Stelle der bisherigen Programmdatei.</summary>
    Applied,

    Refused,
    DownloadFailed,
    ReplaceFailed
}

public sealed record UpdateInstallResult(UpdateInstallStatus Status, string Message);

/// <summary>
/// Lädt eine Veröffentlichung herunter und setzt sie an die Stelle der bisherigen Programmdatei.
///
/// <para>
/// Der Ablauf hat zwei Hälften, die in zwei verschiedenen Prozessen laufen. Die laufende Anwendung
/// <b>stellt bereit</b> (<see cref="StageAsync"/>): sie lädt Programmdatei und Fensterhelfer in ein
/// eigenes Verzeichnis und prüft beide an Grösse und Prüfsumme. Die bereitgestellte Programmdatei wird
/// dann als eigener Prozess gestartet, wartet, bis die alte Anwendung beendet ist, und <b>übernimmt</b>
/// (<see cref="Apply"/>): erst dann wird die bisherige Programmdatei beiseitegeschoben und die neue an
/// ihren Platz gelegt.
/// </para>
///
/// <para>
/// Diese Reihenfolge ist zwingend. Eine Single-File-Anwendung lädt viele ihrer Bausteine erst bei
/// Bedarf aus der eigenen Programmdatei nach — und zwar über deren Pfad. Wird die Datei unter dem
/// laufenden Prozess weggeschoben, scheitert jedes spätere Nachladen mit einer
/// <c>FileNotFoundException</c>, meist Minuten später an einer scheinbar unbeteiligten Stelle. Bis zum
/// 04.09.2026 wurde die laufende Datei sofort nach dem Download ersetzt; die Abstürze folgten beim
/// Beenden, beim ersten Fehlerdialog und bei der nächsten Updatesuche.
/// </para>
///
/// <para>
/// Windows lässt eine laufende Programmdatei nicht überschreiben, wohl aber umbenennen. Der Austausch
/// geht deshalb in drei Schritten: die neue Datei landet zuerst daneben, dann wird die bisherige
/// beiseitegeschoben, dann die neue an ihren Platz gelegt. Bricht ein Schritt ab, wird der vorherige
/// Zustand wiederhergestellt — es darf nie eine halb ersetzte Programmdatei zurückbleiben. Bringt die
/// Veröffentlichung den Fensterhelfer mit, wird er im selben Zug ersetzt: erst der Helfer, dann die
/// Programmdatei. Scheitert der zweite Schritt, wandert auch der Helfer zurück.
/// </para>
///
/// <para>
/// Beiseitegeschobene Dateien und das Bereitstellungsverzeichnis räumt der nächste Start weg
/// (<see cref="RemoveSupersededFiles"/>, <see cref="CleanStagingDirectory"/>). Was sich nicht löschen
/// lässt, bleibt liegen und stört nicht.
/// </para>
/// </summary>
public sealed class UpdateInstaller
{
    private const string SupersededMarker = ".previous.";
    private const string DownloadSuffix = ".download";
    private readonly Func<HttpClient> clientFactory;

    public UpdateInstaller(Func<HttpClient>? clientFactory = null)
    {
        this.clientFactory = clientFactory ?? (() => new HttpClient { Timeout = TimeSpan.FromMinutes(10) });
    }

    /// <summary>
    /// Lädt Programmdatei und, sofern vorhanden, Fensterhelfer der Veröffentlichung in das
    /// Bereitstellungsverzeichnis. Die laufende Programmdatei bleibt unangetastet.
    /// </summary>
    public async Task<UpdateInstallResult> StageAsync(
        string stagingDirectory,
        ReleaseDescription release,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        ArgumentNullException.ThrowIfNull(release);

        if (!UpdateCheck.IsAcceptableDownload(release, out var rejection))
        {
            return new UpdateInstallResult(UpdateInstallStatus.Refused, rejection);
        }

        try
        {
            // Reste einer frueheren, nie uebernommenen Bereitstellung duerfen nicht mit den neuen Dateien
            // vermischt werden.
            CleanStagingDirectory(stagingDirectory);
            Directory.CreateDirectory(stagingDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new UpdateInstallResult(
                UpdateInstallStatus.DownloadFailed,
                $"Das Bereitstellungsverzeichnis liess sich nicht anlegen: {exception.Message}");
        }

        var stagedExecutable = BuildStagedExecutablePath(stagingDirectory);
        if (await FetchAsync(
                release.DownloadUrl,
                release.SizeInBytes,
                release.ChecksumUrl!,
                stagedExecutable,
                cancellationToken).ConfigureAwait(false) is { } failure)
        {
            return failure;
        }

        if (UpdateCheck.HasHelper(release))
        {
            var stagedHelper = BuildStagedHelperPath(stagingDirectory);
            if (await FetchAsync(
                    release.HelperUrl!,
                    release.HelperSizeInBytes,
                    release.HelperChecksumUrl!,
                    stagedHelper,
                    cancellationToken).ConfigureAwait(false) is { } helperFailure)
            {
                TryDelete(stagedExecutable);
                return helperFailure;
            }
        }

        return new UpdateInstallResult(
            UpdateInstallStatus.Staged,
            "Die neue Version liegt bereit. Sie wird nach dem Beenden übernommen und gestartet.");
    }

    /// <summary>
    /// Legt die bereitgestellten Dateien an die Stelle der bisherigen. Läuft im Prozess der neuen
    /// Programmdatei, nachdem die alte Anwendung beendet ist. Die bereitgestellten Dateien werden
    /// kopiert, nicht verschoben: die Programmdatei, aus der dieser Prozess läuft, darf nicht unter ihm
    /// weggeschoben werden.
    /// </summary>
    public static UpdateInstallResult Apply(
        string stagingDirectory,
        string targetExecutablePath,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetExecutablePath);

        var stagedExecutable = BuildStagedExecutablePath(stagingDirectory);
        if (!File.Exists(stagedExecutable))
        {
            return new UpdateInstallResult(
                UpdateInstallStatus.DownloadFailed,
                "Die bereitgestellte Programmdatei ist nicht mehr vorhanden.");
        }

        var download = targetExecutablePath + DownloadSuffix;
        var stagedHelper = BuildStagedHelperPath(stagingDirectory);
        string? helperPath = null;
        string? helperDownload = null;
        try
        {
            File.Copy(stagedExecutable, download, overwrite: true);
            if (File.Exists(stagedHelper))
            {
                helperPath = BuildHelperPath(targetExecutablePath);
                helperDownload = helperPath + DownloadSuffix;
                File.Copy(stagedHelper, helperDownload, overwrite: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            TryDelete(download);
            if (helperDownload is not null)
            {
                TryDelete(helperDownload);
            }

            return new UpdateInstallResult(
                UpdateInstallStatus.ReplaceFailed,
                $"Die neue Version liess sich nicht neben die Programmdatei legen: {exception.Message}");
        }

        return ReplaceAll(targetExecutablePath, download, helperPath, helperDownload, now);
    }

    /// <summary>Der Fensterhelfer liegt neben der Programmdatei.</summary>
    public static string BuildHelperPath(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return Path.Combine(
            Path.GetDirectoryName(executablePath) ?? string.Empty,
            InstallationPlan.HelperName);
    }

    public static string BuildStagedExecutablePath(string stagingDirectory) =>
        Path.Combine(stagingDirectory, InstallationPlan.ExecutableName);

    public static string BuildStagedHelperPath(string stagingDirectory) =>
        Path.Combine(stagingDirectory, InstallationPlan.HelperName);

    /// <summary>
    /// Entfernt das Bereitstellungsverzeichnis samt Inhalt. Liefert <c>true</c>, wenn danach nichts mehr
    /// davon übrig ist. Läuft gerade die bereitgestellte Programmdatei — etwa während sie das Update
    /// übernimmt —, lässt sie sich nicht löschen und bleibt bis zum nächsten Start liegen.
    /// </summary>
    public static bool CleanStagingDirectory(string stagingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        if (!Directory.Exists(stagingDirectory))
        {
            return true;
        }

        var clean = true;
        foreach (var file in Directory.EnumerateFiles(stagingDirectory))
        {
            clean &= TryDelete(file);
        }

        if (clean)
        {
            try
            {
                Directory.Delete(stagingDirectory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                clean = false;
            }
        }

        return clean;
    }

    /// <summary>
    /// Lädt eine Datei der Veröffentlichung und prüft sie an Grösse und Prüfsumme. Gibt <c>null</c>
    /// zurück, wenn die Datei einwandfrei am Ziel liegt, sonst den Grund des Abbruchs; die halbe Datei
    /// ist dann bereits entfernt.
    /// </summary>
    private async Task<UpdateInstallResult?> FetchAsync(
        string url,
        long expectedSize,
        string checksumUrl,
        string downloadPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await DownloadAsync(url, downloadPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            TryDelete(downloadPath);
            return new UpdateInstallResult(
                UpdateInstallStatus.DownloadFailed,
                $"Der Download ist fehlgeschlagen: {exception.Message}");
        }

        var actualSize = new FileInfo(downloadPath).Length;
        if (actualSize != expectedSize)
        {
            // Eine abgebrochene Uebertragung sieht wie eine vollstaendige Datei aus.
            TryDelete(downloadPath);
            return new UpdateInstallResult(
                UpdateInstallStatus.DownloadFailed,
                $"Die geladene Datei ist {actualSize} statt {expectedSize} Bytes gross und wird verworfen.");
        }

        // Die Pruefsumme kommt aus einer zweiten Datei derselben Veroeffentlichung. Wer die Programmdatei
        // unterschieben will, muesste auch sie ersetzen; die Groesse allein hielt niemanden auf.
        string expectedChecksum;
        try
        {
            var checksumContent = await DownloadTextAsync(checksumUrl, cancellationToken).ConfigureAwait(false);
            if (!UpdateCheck.TryParseChecksum(checksumContent, out expectedChecksum))
            {
                TryDelete(downloadPath);
                return new UpdateInstallResult(
                    UpdateInstallStatus.DownloadFailed,
                    "Die Prüfsummendatei der Veröffentlichung ist nicht lesbar; die Datei wird verworfen.");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TryDelete(downloadPath);
            return new UpdateInstallResult(
                UpdateInstallStatus.DownloadFailed,
                $"Die Prüfsumme liess sich nicht laden: {exception.Message}");
        }

        var actualChecksum = await ComputeChecksumAsync(downloadPath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(downloadPath);
            return new UpdateInstallResult(
                UpdateInstallStatus.DownloadFailed,
                "Die Prüfsumme der geladenen Datei stimmt nicht mit der Veröffentlichung überein; die Datei wird verworfen.");
        }

        return null;
    }

    /// <summary>SHA-256 einer Datei als Hexadezimalzeichen in Kleinbuchstaben.</summary>
    public static async Task<string> ComputeChecksumAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task<string> DownloadTextAsync(string url, CancellationToken cancellationToken)
    {
        using var client = clientFactory();
        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > 4096)
        {
            throw new InvalidDataException("Die Prüfsummendatei ist unplausibel gross.");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Legt die geladene Datei an die Stelle der bisherigen. Der Zeitpunkt wird hereingereicht, damit der
    /// Name der beiseitegeschobenen Datei prüfbar bleibt.
    /// </summary>
    public static UpdateInstallResult Replace(
        string executablePath,
        string downloadPath,
        DateTimeOffset now) =>
        Replace(executablePath, downloadPath, now, out _);

    /// <summary>
    /// Ersetzt Programmdatei und Fensterhelfer als ein Vorgang. Der Helfer geht zuerst, weil sich sein
    /// Austausch noch folgenlos zurücknehmen lässt; scheitert danach die Programmdatei, wandert er
    /// zurück. Ohne Helfer verhält sich der Aufruf wie <see cref="Replace(string, string, DateTimeOffset)"/>.
    /// </summary>
    public static UpdateInstallResult ReplaceAll(
        string executablePath,
        string downloadPath,
        string? helperPath,
        string? helperDownloadPath,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadPath);

        if (helperPath is not { Length: > 0 } || helperDownloadPath is not { Length: > 0 })
        {
            return Replace(executablePath, downloadPath, now);
        }

        var helperResult = Replace(helperPath, helperDownloadPath, now, out var helperSupersededPath);
        if (helperResult.Status != UpdateInstallStatus.Applied)
        {
            TryDelete(downloadPath);
            return helperResult with
            {
                Message = $"Der Fensterhelfer liess sich nicht ersetzen: {helperResult.Message}",
            };
        }

        var result = Replace(executablePath, downloadPath, now);
        if (result.Status != UpdateInstallStatus.Applied)
        {
            // Der Helfer ist schon neu, die Anwendung nicht: dieser Stand wird zurueckgenommen, damit
            // keine Paarung aus alter Anwendung und neuem Helfer entsteht.
            TryDelete(helperPath);
            if (helperSupersededPath is { Length: > 0 })
            {
                TryMoveBack(helperSupersededPath, helperPath);
            }
        }

        return result;
    }

    private static UpdateInstallResult Replace(
        string executablePath,
        string downloadPath,
        DateTimeOffset now,
        out string? supersededPathIfMoved)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadPath);
        supersededPathIfMoved = null;

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
            supersededPathIfMoved = moved ? supersededPath : null;
            return new UpdateInstallResult(
                UpdateInstallStatus.Applied,
                "Die neue Version liegt an ihrem Platz.");
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
        string url,
        string downloadPath,
        CancellationToken cancellationToken)
    {
        using var client = clientFactory();
        using var response = await client
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
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

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
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
