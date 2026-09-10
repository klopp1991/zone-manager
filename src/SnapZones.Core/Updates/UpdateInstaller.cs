using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;

namespace SnapZones.Core.Updates;

public enum UpdateInstallStatus
{
    /// <summary>Das geprüfte Installationspaket liegt im Bereitstellungsverzeichnis.</summary>
    Staged,

    Refused,
    DownloadFailed
}

/// <param name="PackagePath">Das bereitgestellte Installationspaket, sobald der Status <c>Staged</c> ist.</param>
public sealed record UpdateInstallResult(UpdateInstallStatus Status, string Message, string? PackagePath = null);

/// <summary>
/// Lädt das Installationspaket einer Veröffentlichung herunter und prüft es.
///
/// <para>
/// An einer Veröffentlichung hängt seit dem 10.09.2026 genau eine Datei: <c>ZoneManager-Setup-…msi</c>.
/// Damit fällt der frühere Austausch der laufenden Programmdatei weg — mit ihm das ganze Verfahren aus
/// Beiseiteschieben, Ersetzen und Zurückrollen, das nötig war, weil eine Single-File-Anwendung ihre
/// Bausteine über den Pfad der eigenen Programmdatei nachlädt und unter sich nichts ausgetauscht
/// bekommen darf. Das Paket übernimmt das: Windows Installer ersetzt Programmdatei und Fensterhelfer
/// gemeinsam und hält den Eintrag in «Apps und Features» nach.
/// </para>
///
/// <para>
/// Geladen wird in ein eigenes Verzeichnis und erst dann geprüft: Grösse gegen die Angabe der
/// Veröffentlichung, Inhalt gegen die SHA-256-Prüfsumme aus ihrem Text. Passt etwas nicht, wird die
/// Datei gelöscht und nichts übernommen. Der Aufrufer startet das geprüfte Paket, siehe
/// <c>UpdateCoordinator.TryLaunchInstaller</c>.
/// </para>
/// </summary>
public sealed class UpdateInstaller
{
    private readonly Func<HttpClient> clientFactory;
    private readonly Func<string?> accessToken;

    public UpdateInstaller(Func<HttpClient>? clientFactory = null, Func<string?>? accessToken = null)
    {
        this.clientFactory = clientFactory ?? (() => new HttpClient { Timeout = TimeSpan.FromMinutes(10) });
        this.accessToken = accessToken ?? (() => null);
    }

    /// <summary>
    /// Lädt das Installationspaket und prüft es. Erfolgreich heisst: die Datei liegt geprüft im
    /// Bereitstellungsverzeichnis und lässt sich starten.
    /// </summary>
    public async Task<UpdateInstallResult> StageAsync(
        ReleaseDescription release,
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);

        if (!UpdateCheck.IsAcceptableDownload(release, out var rejection))
        {
            return new UpdateInstallResult(UpdateInstallStatus.Refused, rejection);
        }

        if (!UpdateCheck.TryParseChecksum(release.Checksum, out var expected))
        {
            return new UpdateInstallResult(
                UpdateInstallStatus.Refused,
                "Die Veröffentlichung nennt keine SHA-256-Prüfsumme und wird nicht geladen.");
        }

        var packagePath = Path.Combine(
            stagingDirectory,
            $"ZoneManager-Setup-{Sanitize(release.TagName)}{UpdateCheck.PackageExtension}");
        try
        {
            Directory.CreateDirectory(stagingDirectory);
            TryDelete(packagePath);
            await DownloadAsync(release.DownloadUrl, packagePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TryDelete(packagePath);
            return new UpdateInstallResult(
                UpdateInstallStatus.DownloadFailed,
                $"Das Installationspaket liess sich nicht laden: {exception.Message}");
        }

        var actualSize = new FileInfo(packagePath).Length;
        if (actualSize != release.SizeInBytes)
        {
            TryDelete(packagePath);
            return new UpdateInstallResult(
                UpdateInstallStatus.Refused,
                "Das geladene Installationspaket hat nicht die angekündigte Grösse. Die Datei wurde nicht übernommen.");
        }

        var actual = await ComputeChecksumAsync(packagePath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(packagePath);
            return new UpdateInstallResult(
                UpdateInstallStatus.Refused,
                "Die Prüfsumme des Installationspakets stimmt nicht. Die Datei wurde nicht übernommen.");
        }

        return new UpdateInstallResult(
            UpdateInstallStatus.Staged,
            "Das Installationspaket ist geladen und geprüft.",
            packagePath);
    }

    /// <summary>
    /// Räumt das Bereitstellungsverzeichnis. Was sich nicht löschen lässt, bleibt liegen und stört nicht;
    /// der nächste Lauf überschreibt es.
    /// </summary>
    public static void CleanStagingDirectory(string stagingDirectory)
    {
        if (string.IsNullOrWhiteSpace(stagingDirectory) || !Directory.Exists(stagingDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(stagingDirectory))
        {
            TryDelete(file);
        }
    }

    /// <summary>
    /// Entfernt die beiseitegeschobenen Dateien des früheren Austauschverfahrens. Sie entstehen seit dem
    /// 10.09.2026 nicht mehr; auf Rechnern, die vorher ein Update bekommen haben, liegen sie noch.
    /// </summary>
    public static int RemoveSupersededFiles(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return 0;
        }

        var directory = Path.GetDirectoryName(executablePath);
        var name = Path.GetFileName(executablePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return 0;
        }

        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(directory, $"{name}.previous.*"))
        {
            if (TryDelete(file))
            {
                removed++;
            }
        }

        return removed;
    }

    private async Task DownloadAsync(string url, string downloadPath, CancellationToken cancellationToken)
    {
        using var client = clientFactory();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        // Die API-Adresse eines Anhangs liefert die Datei nur mit diesem Accept-Kopf; ohne ihn kaeme
        // die JSON-Beschreibung. Der Zugangsschluessel oeffnet die Ablage eines privaten Repositories.
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/octet-stream"));
        if (accessToken() is { Length: > 0 } token)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
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

    private static async Task<string> ComputeChecksumAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Der Tag als Dateiname; ein Tag darf Zeichen tragen, die ein Pfad nicht verträgt.</summary>
    private static string Sanitize(string tag)
    {
        var cleaned = new string(tag.Where(character =>
            char.IsLetterOrDigit(character) || character is '.' or '-' or '_').ToArray());
        return cleaned.Length == 0
            ? DateTimeOffset.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
            : cleaned;
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
