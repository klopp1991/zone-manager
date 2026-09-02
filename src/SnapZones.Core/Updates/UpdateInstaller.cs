using System.Globalization;
using System.Net.Http;
using SnapZones.Core.Setup;

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
/// Bringt die Veröffentlichung den Fensterhelfer mit, wird er im selben Zug ersetzt: erst der Helfer,
/// dann die Programmdatei. Scheitert der zweite Schritt, wandert auch der Helfer zurück — sonst liefe
/// eine alte Anwendung gegen einen neuen Helfer, also genau die Paarung, die dieser Ablauf verhindern
/// soll.
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
        if (await FetchAsync(
                release.DownloadUrl,
                release.SizeInBytes,
                release.ChecksumUrl!,
                downloadPath,
                cancellationToken).ConfigureAwait(false) is { } failure)
        {
            return failure;
        }

        // Der Fensterhelfer wird vor dem Austausch vollstaendig geladen und geprueft. Erst wenn beide
        // Dateien fertig danebenliegen, wird etwas ersetzt.
        string? helperPath = null;
        string? helperDownloadPath = null;
        if (UpdateCheck.HasHelper(release))
        {
            helperPath = BuildHelperPath(executablePath);
            helperDownloadPath = helperPath + ".download";
            if (await FetchAsync(
                    release.HelperUrl!,
                    release.HelperSizeInBytes,
                    release.HelperChecksumUrl!,
                    helperDownloadPath,
                    cancellationToken).ConfigureAwait(false) is { } helperFailure)
            {
                TryDelete(downloadPath);
                return helperFailure;
            }
        }

        return ReplaceAll(
            executablePath,
            downloadPath,
            helperPath,
            helperDownloadPath,
            TimeProvider.System.GetUtcNow());
    }

    /// <summary>Der Fensterhelfer liegt neben der Programmdatei.</summary>
    public static string BuildHelperPath(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return Path.Combine(
            Path.GetDirectoryName(executablePath) ?? string.Empty,
            InstallationPlan.HelperName);
    }

    /// <summary>
    /// Lädt eine Datei der Veröffentlichung und prüft sie an Grösse und Prüfsumme. Gibt <c>null</c>
    /// zurück, wenn die Datei einwandfrei danebenliegt, sonst den Grund des Abbruchs; die halbe Datei
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
    /// Legt die geladene Datei an die Stelle der laufenden. Der Zeitpunkt wird hereingereicht, damit der
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
