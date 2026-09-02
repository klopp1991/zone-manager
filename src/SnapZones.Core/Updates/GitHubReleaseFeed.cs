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
/// Die Abfrage geht ohne Anmeldung und ohne Kennung: es wird nichts gesendet ausser der Anfrage selbst,
/// keine Version, keine Rechnerkennung, keine Zählung. Die Antwort wird auf die drei benötigten Angaben
/// eingedampft — Tag, Adresse und Grösse der Programmdatei — und alles andere verworfen.
/// </summary>
public sealed class GitHubReleaseFeed : IReleaseFeed
{
    public const string DefaultEndpoint =
        "https://api.github.com/repos/klopp1991/zone-manager/releases/latest";

    private const string AssetName = "ZoneManager.exe";
    private const string ChecksumAssetName = "ZoneManager.exe.sha256";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private readonly Func<HttpClient> clientFactory;
    private readonly string endpoint;
    private readonly string userAgent;

    public GitHubReleaseFeed(string userAgent, string? endpoint = null, Func<HttpClient>? clientFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);
        this.userAgent = userAgent;
        this.endpoint = endpoint ?? DefaultEndpoint;
        this.clientFactory = clientFactory ?? (() => new HttpClient { Timeout = RequestTimeout });
    }

    public async Task<ReleaseDescription?> ReadLatestAsync(CancellationToken cancellationToken)
    {
        using var client = clientFactory();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

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

    /// <summary>Zieht die drei benötigten Angaben aus der Antwort. Fehlt eine, gibt es kein Ergebnis.</summary>
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
        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.ValueKind != JsonValueKind.Object ||
                !asset.TryGetProperty("name", out var name) ||
                name.ValueKind != JsonValueKind.String ||
                !asset.TryGetProperty("browser_download_url", out var url) ||
                url.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            if (string.Equals(name.GetString(), ChecksumAssetName, StringComparison.OrdinalIgnoreCase))
            {
                checksumUrl = url.GetString();
                continue;
            }

            if (string.Equals(name.GetString(), AssetName, StringComparison.OrdinalIgnoreCase) &&
                asset.TryGetProperty("size", out var size) &&
                size.TryGetInt64(out var parsedSize))
            {
                downloadUrl = url.GetString();
                sizeInBytes = parsedSize;
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
            checksumUrl);
    }
}
