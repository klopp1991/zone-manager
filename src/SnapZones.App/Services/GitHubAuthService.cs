using System.Net.Http;
using System.Net.Http.Headers;
using SnapZones.Core.Updates;
using SnapZones.Windows.Credentials;

namespace SnapZones.App.Services;

/// <summary>Was der Dialog gerade zeigen soll, waehrend auf die Bestaetigung gewartet wird.</summary>
/// <param name="Message">Ein Satz in Alltagssprache.</param>
/// <param name="Remaining">Wie lange der Code noch gilt.</param>
public sealed record GitHubConnectProgress(string Message, TimeSpan Remaining);

/// <summary>Wie das Verbinden ausgegangen ist.</summary>
/// <param name="Connected">Ob ein Zugangsschluessel hinterlegt wurde.</param>
/// <param name="AccountName">Der Kontoname, unter dem die Verbindung steht.</param>
/// <param name="Message">Ein Satz in Alltagssprache; bei Fehlschlag der Grund.</param>
public sealed record GitHubConnectResult(bool Connected, string AccountName, string Message);

/// <summary>
/// Verbindet Zone Manager mit einem GitHub-Konto, damit die Veroeffentlichungen aus dem privaten
/// Repository des Projekts gelesen werden duerfen.
///
/// <para>
/// Ueber die Browsersitzung geht das nicht: eine Anwendung kann keine GitHub-Sitzung des Browsers
/// benutzen. Der Geraetecode ist der offizielle Weg dafuer und braucht nur einen Klick im Browser.
/// Der Zugangsschluessel liegt danach im Anmeldeinformations-Speicher von Windows, nie in einer
/// Datei und nie im Protokoll.
/// </para>
/// </summary>
public sealed class GitHubAuthService
{
    /// <summary>Das Ziel im Anmeldeinformations-Speicher.</summary>
    public const string CredentialTarget = "ZoneManager:GitHub";

    /// <summary>
    /// Die Client-Kennung der OAuth-Anwendung des Projekts. Sie ist oeffentlich, kein Geheimnis. Solange
    /// hier nichts steht, gibt es keine OAuth-Anwendung: dann fuehrt nur der Weg ueber ein selbst
    /// erzeugtes Zugriffstoken, und der Dialog bietet gleich diesen an.
    /// </summary>
    public const string ClientId = "";

    /// <summary>Ob eine OAuth-Anwendung hinterlegt ist und der Geraetecode damit ueberhaupt geht.</summary>
    public static bool HasDeviceFlow => ClientId.Length > 0;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
    private readonly Func<HttpClient> clientFactory;
    private readonly string userAgent;
    private readonly Action<string, string, Exception?> log;

    public GitHubAuthService(
        string userAgent,
        Action<string, string, Exception?> log,
        Func<HttpClient>? clientFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);
        this.userAgent = userAgent;
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.clientFactory = clientFactory ?? (() => new HttpClient { Timeout = RequestTimeout });
    }

    /// <summary>Der hinterlegte Zugangsschluessel, oder <c>null</c>.</summary>
    public static string? ReadToken()
    {
        try
        {
            return WindowsCredentialStore.Read(CredentialTarget);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>Der Kontoname der Verbindung, oder eine leere Zeichenkette.</summary>
    public static string ReadAccountName()
    {
        try
        {
            var name = WindowsCredentialStore.ReadUserName(CredentialTarget);
            return string.IsNullOrWhiteSpace(name) || name == CredentialTarget ? string.Empty : name;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>Ob eine Verbindung besteht.</summary>
    public static bool IsConnected() => ReadToken() is { Length: > 0 };

    /// <summary>Nimmt den Zugangsschluessel wieder heraus.</summary>
    public void Disconnect()
    {
        try
        {
            WindowsCredentialStore.Delete(CredentialTarget);
            log("INFO", "Die Verbindung zu GitHub wurde getrennt.", null);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception)
        {
            log("WARN", "Der Zugangsschlüssel liess sich nicht entfernen.", exception);
        }
    }

    /// <summary>
    /// Legt einen von Hand eingefuegten Zugangsschluessel ab, etwa ein fein abgestuftes persoenliches
    /// Token mit ausschliesslich lesendem Zugriff auf dieses Repository.
    /// </summary>
    public async Task<GitHubConnectResult> ConnectWithTokenAsync(string token, CancellationToken cancellationToken)
    {
        var trimmed = token?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return new GitHubConnectResult(false, string.Empty, "Es wurde kein Zugriffstoken eingefügt.");
        }

        var account = await ReadAccountAsync(trimmed, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return new GitHubConnectResult(false, string.Empty, "Mit diesem Zugriffstoken meldet sich GitHub nicht.");
        }

        return Store(trimmed, account);
    }

    /// <summary>
    /// Fordert einen Geraetecode an; <c>null</c>, wenn es keine OAuth-Anwendung gibt oder GitHub nicht
    /// erreichbar ist.
    /// </summary>
    public async Task<GitHubDeviceCode?> RequestDeviceCodeAsync(CancellationToken cancellationToken)
    {
        if (!HasDeviceFlow)
        {
            return null;
        }

        try
        {
            var json = await PostAsync(
                GitHubDeviceFlow.DeviceCodeEndpoint,
                GitHubDeviceFlow.DeviceCodeRequest(ClientId),
                cancellationToken).ConfigureAwait(false);
            return GitHubDeviceFlow.ParseDeviceCode(json);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            log("WARN", "Der Gerätecode liess sich nicht anfordern.", exception);
            return null;
        }
    }

    /// <summary>
    /// Fragt im vorgegebenen Takt nach, bis der Benutzer den Code bestaetigt hat, der Code ablaeuft oder
    /// der Aufrufer abbricht. Jeder Zwischenstand geht an <paramref name="report"/>.
    /// </summary>
    public async Task<GitHubConnectResult> AwaitApprovalAsync(
        GitHubDeviceCode code,
        IProgress<GitHubConnectProgress>? report,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        var interval = code.Interval;
        var deadline = DateTimeOffset.UtcNow + code.ExpiresIn;
        while (DateTimeOffset.UtcNow < deadline)
        {
            report?.Report(new GitHubConnectProgress(
                "Warte auf die Bestätigung …",
                deadline - DateTimeOffset.UtcNow));
            try
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return new GitHubConnectResult(false, string.Empty, "Die Anmeldung wurde abgebrochen.");
            }

            string json;
            try
            {
                json = await PostAsync(
                    GitHubDeviceFlow.AccessTokenEndpoint,
                    GitHubDeviceFlow.AccessTokenRequest(ClientId, code.DeviceCode),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                return new GitHubConnectResult(false, string.Empty, "GitHub ist gerade nicht erreichbar.");
            }

            var poll = GitHubDeviceFlow.ParsePollResponse(json, interval);
            interval = poll.Interval;
            switch (poll.Outcome)
            {
                case GitHubPollOutcome.Granted when poll.AccessToken is { Length: > 0 } token:
                    var account = await ReadAccountAsync(token, cancellationToken).ConfigureAwait(false);
                    return Store(token, account ?? "GitHub");

                case GitHubPollOutcome.Pending:
                case GitHubPollOutcome.SlowDown:
                    continue;

                default:
                    return new GitHubConnectResult(false, string.Empty, poll.Message);
            }
        }

        return new GitHubConnectResult(false, string.Empty, "Der Code ist abgelaufen. Fordere einen neuen an.");
    }

    /// <summary>Der Kontoname zum Zugangsschluessel, oder <c>null</c>, wenn GitHub ihn nicht nennt.</summary>
    private async Task<string?> ReadAccountAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            using var client = clientFactory();
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
            request.Headers.UserAgent.ParseAdd(userAgent);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = System.Text.Json.JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("login", out var login) &&
                login.ValueKind == System.Text.Json.JsonValueKind.String
                ? login.GetString()
                : null;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private GitHubConnectResult Store(string token, string account)
    {
        try
        {
            WindowsCredentialStore.Write(CredentialTarget, account, token);
            // Der Schluessel selbst darf nie ins Protokoll; der Kontoname genuegt.
            log("INFO", $"Mit GitHub verbunden als «{account}».", null);
            return new GitHubConnectResult(true, account, $"Verbunden als «{account}».");
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception)
        {
            log("ERROR", "Der Zugangsschlüssel liess sich nicht ablegen.", exception);
            return new GitHubConnectResult(false, string.Empty, "Der Zugangsschlüssel liess sich nicht ablegen.");
        }
    }

    private async Task<string> PostAsync(
        string endpoint,
        IReadOnlyList<KeyValuePair<string, string>> fields,
        CancellationToken cancellationToken)
    {
        using var client = clientFactory();
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(fields)
        };
        request.Headers.UserAgent.ParseAdd(userAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }
}
