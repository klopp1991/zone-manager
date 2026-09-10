using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SnapZones.Core.Updates;

public interface IReleaseFeed
{
    /// <summary>
    /// Liest die neueste Veröffentlichung. Gibt <c>null</c> zurück, wenn keine erreichbar oder lesbar
    /// ist; ein Fehlschlag ist kein Ausnahmefall, sondern der Normalfall ohne Netzwerk.
    /// </summary>
    Task<ReleaseDescription?> ReadLatestAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Liest die neueste Veröffentlichung aus der Release-Ablage des Projekts.
///
/// Die Abfrage sendet nichts ausser der Anfrage selbst — keine Version, keine Rechnerkennung, keine
/// Zählung. Liegt ein Zugangsschlüssel vor, geht er als <c>Authorization: Bearer</c> mit; nur so ist
/// die Release-Ablage eines privaten Repositories überhaupt lesbar. Die Antwort wird eingedampft —
/// Tag sowie Adresse und Grösse der Programmdatei, des Fensterhelfers und der beiden Prüfsummendateien —
/// und alles andere verworfen.
/// </summary>
public sealed class GitHubReleaseFeed : IReleaseFeed
{
    public const string DefaultEndpoint =
        "https://api.github.com/repos/klopp1991/zone-manager/releases/latest";

    private const string AssetName = "ZoneManager.exe";
    private const string ChecksumAssetName = "ZoneManager.exe.sha256";
    private const string HelperAssetName = "ZoneManager.Helper.exe";
    private const string HelperChecksumAssetName = "ZoneManager.Helper.exe.sha256";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private readonly Func<HttpClient> clientFactory;
    private readonly string endpoint;
    private readonly string userAgent;
    private readonly Func<string?> accessToken;

    public GitHubReleaseFeed(
        string userAgent,
        string? endpoint = null,
        Func<HttpClient>? clientFactory = null,
        Func<string?>? accessToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);
        this.userAgent = userAgent;
        this.endpoint = endpoint ?? DefaultEndpoint;
        this.clientFactory = clientFactory ?? (() => new HttpClient { Timeout = RequestTimeout });
        this.accessToken = accessToken ?? (() => null);
    }

    public async Task<ReleaseDescription?> ReadLatestAsync(CancellationToken cancellationToken)
    {
        using var client = clientFactory();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (accessToken() is { Length: > 0 } token)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Parse(document.RootElement);
    }

    /// <summary>
    /// Zieht die benötigten Angaben aus der Antwort. Fehlt die Programmdatei, gibt es kein Ergebnis;
    /// fehlt der Fensterhelfer, bleibt der vorhandene liegen — ältere Veröffentlichungen tragen ihn nicht.
    /// </summary>
    public static ReleaseDescription? Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("tag_name", out var tag) ||
            tag.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("assets", out var assets) ||
            assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        // Ein Entwurf ist noch nicht veroeffentlicht und wird nie angeboten.
        if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
        {
            return null;
        }

        string? downloadUrl = null;
        string? checksumUrl = null;
        long sizeInBytes = 0;
        string? helperUrl = null;
        string? helperChecksumUrl = null;
        long helperSizeInBytes = 0;
        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.ValueKind != JsonValueKind.Object ||
                !asset.TryGetProperty("name", out var name) ||
                name.ValueKind != JsonValueKind.String ||
                AssetUrl(asset) is not { Length: > 0 } assetUrl)
            {
                continue;
            }

            var assetName = name.GetString();
            if (string.Equals(assetName, ChecksumAssetName, StringComparison.OrdinalIgnoreCase))
            {
                checksumUrl = assetUrl;
                continue;
            }

            if (string.Equals(assetName, HelperChecksumAssetName, StringComparison.OrdinalIgnoreCase))
            {
                helperChecksumUrl = assetUrl;
                continue;
            }

            if (!asset.TryGetProperty("size", out var size) || !size.TryGetInt64(out var parsedSize))
            {
                continue;
            }

            if (string.Equals(assetName, AssetName, StringComparison.OrdinalIgnoreCase))
            {
                downloadUrl = assetUrl;
                sizeInBytes = parsedSize;
            }
            else if (string.Equals(assetName, HelperAssetName, StringComparison.OrdinalIgnoreCase))
            {
                helperUrl = assetUrl;
                helperSizeInBytes = parsedSize;
            }
        }

        if (downloadUrl is null)
        {
            return null;
        }

        var notes = root.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String
            ? body.GetString()
            : null;
        return new ReleaseDescription(
            tag.GetString() ?? string.Empty,
            downloadUrl,
            sizeInBytes,
            notes,
            checksumUrl,
            helperUrl,
            helperSizeInBytes,
            helperChecksumUrl);
    }

    /// <summary>
    /// Die Adresse, ueber die eine Datei geladen wird. Bevorzugt die API-Adresse aus <c>url</c>: nur sie
    /// nimmt einen Zugangsschluessel an und leitet auf die signierte Ablage weiter. Erst wenn sie fehlt,
    /// gilt <c>browser_download_url</c>, der nur bei oeffentlichen Repositories traegt.
    /// </summary>
    private static string? AssetUrl(JsonElement asset)
    {
        if (asset.TryGetProperty("url", out var apiUrl) && apiUrl.ValueKind == JsonValueKind.String)
        {
            return apiUrl.GetString();
        }

        return asset.TryGetProperty("browser_download_url", out var browserUrl) &&
            browserUrl.ValueKind == JsonValueKind.String
            ? browserUrl.GetString()
            : null;
    }
}
