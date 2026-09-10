using System.Globalization;
using System.Text.Json;

namespace SnapZones.Core.Updates;

/// <summary>Der Geraetecode, den GitHub ausgibt, und alles, was der Dialog dazu zeigen muss.</summary>
/// <param name="UserCode">Der Code, den der Benutzer im Browser eintippt.</param>
/// <param name="DeviceCode">Die Kennung, mit der das Programm nach der Bestaetigung fragt.</param>
/// <param name="VerificationUri">Die Seite, auf der der Code bestaetigt wird.</param>
/// <param name="Interval">Wie lange zwischen zwei Nachfragen gewartet werden muss.</param>
/// <param name="ExpiresIn">Wie lange der Code gilt.</param>
public sealed record GitHubDeviceCode(
    string UserCode,
    string DeviceCode,
    string VerificationUri,
    TimeSpan Interval,
    TimeSpan ExpiresIn);

/// <summary>Wie eine Nachfrage nach dem Zugangsschluessel ausgegangen ist.</summary>
public enum GitHubPollOutcome
{
    /// <summary>Der Benutzer hat den Code noch nicht bestaetigt; weiter warten.</summary>
    Pending,

    /// <summary>Es wurde zu schnell nachgefragt; das Intervall wird groesser.</summary>
    SlowDown,

    /// <summary>Der Code ist abgelaufen; es braucht einen neuen.</summary>
    Expired,

    /// <summary>Der Benutzer hat abgelehnt.</summary>
    Denied,

    /// <summary>Der Zugangsschluessel steht bereit.</summary>
    Granted,

    /// <summary>Etwas anderes ist schiefgegangen; der Text steht in <see cref="GitHubPollResult.Message"/>.</summary>
    Failed
}

/// <param name="Outcome">Wie die Nachfrage ausgegangen ist.</param>
/// <param name="AccessToken">Der Zugangsschluessel, sobald er da ist.</param>
/// <param name="Interval">Das ab jetzt geltende Nachfrageintervall.</param>
/// <param name="Message">Ein Satz in Alltagssprache, den der Dialog anzeigen kann.</param>
public sealed record GitHubPollResult(
    GitHubPollOutcome Outcome,
    string? AccessToken,
    TimeSpan Interval,
    string Message);

/// <summary>
/// Der Geraetecode-Ablauf von GitHub, ohne Netzwerk und ohne Oberflaeche: er formt die beiden Anfragen
/// und liest die Antworten. Das Verfahren ist der offizielle Weg fuer ein Programm ohne eigenen
/// Browser; ueber die Sitzung des Browsers kommt eine Anwendung nicht an ein privates Repository.
/// </summary>
public static class GitHubDeviceFlow
{
    public const string DeviceCodeEndpoint = "https://github.com/login/device/code";
    public const string AccessTokenEndpoint = "https://github.com/login/oauth/access_token";
    public const string GrantType = "urn:ietf:params:oauth:grant-type:device_code";

    /// <summary>Der Umfang, den das Programm braucht: die Dateien der Veroeffentlichungen lesen.</summary>
    public const string Scope = "repo";

    /// <summary>Ohne Angabe von GitHub wird alle fuenf Sekunden nachgefragt.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(5);

    /// <summary>Die Felder der ersten Anfrage.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> DeviceCodeRequest(string clientId) =>
    [
        new("client_id", clientId),
        new("scope", Scope)
    ];

    /// <summary>Die Felder jeder Nachfrage.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> AccessTokenRequest(string clientId, string deviceCode) =>
    [
        new("client_id", clientId),
        new("device_code", deviceCode),
        new("grant_type", GrantType)
    ];

    /// <summary>Liest die Antwort der ersten Anfrage; <c>null</c>, wenn sie nicht taugt.</summary>
    public static GitHubDeviceCode? ParseDeviceCode(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                Text(root, "user_code") is not { Length: > 0 } userCode ||
                Text(root, "device_code") is not { Length: > 0 } deviceCode ||
                Text(root, "verification_uri") is not { Length: > 0 } verificationUri)
            {
                return null;
            }

            return new GitHubDeviceCode(
                userCode,
                deviceCode,
                verificationUri,
                Seconds(root, "interval", DefaultInterval),
                Seconds(root, "expires_in", TimeSpan.FromMinutes(15)));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Liest die Antwort einer Nachfrage und sagt, wie es weitergeht.</summary>
    public static GitHubPollResult ParsePollResponse(string json, TimeSpan currentInterval)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new GitHubPollResult(
                GitHubPollOutcome.Failed,
                null,
                currentInterval,
                "GitHub hat nicht geantwortet.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new GitHubPollResult(
                    GitHubPollOutcome.Failed,
                    null,
                    currentInterval,
                    "GitHub hat unerwartet geantwortet.");
            }

            if (Text(root, "access_token") is { Length: > 0 } token)
            {
                return new GitHubPollResult(
                    GitHubPollOutcome.Granted,
                    token,
                    currentInterval,
                    "Verbunden.");
            }

            return Text(root, "error") switch
            {
                "authorization_pending" => new GitHubPollResult(
                    GitHubPollOutcome.Pending,
                    null,
                    currentInterval,
                    "Warte auf die Bestätigung …"),
                "slow_down" => new GitHubPollResult(
                    GitHubPollOutcome.SlowDown,
                    null,
                    Seconds(root, "interval", currentInterval + TimeSpan.FromSeconds(5)),
                    "Warte auf die Bestätigung …"),
                "expired_token" => new GitHubPollResult(
                    GitHubPollOutcome.Expired,
                    null,
                    currentInterval,
                    "Der Code ist abgelaufen. Fordere einen neuen an."),
                "access_denied" => new GitHubPollResult(
                    GitHubPollOutcome.Denied,
                    null,
                    currentInterval,
                    "Die Anmeldung wurde abgebrochen."),
                var other => new GitHubPollResult(
                    GitHubPollOutcome.Failed,
                    null,
                    currentInterval,
                    Describe(other, Text(root, "error_description")))
            };
        }
        catch (JsonException)
        {
            return new GitHubPollResult(
                GitHubPollOutcome.Failed,
                null,
                currentInterval,
                "GitHub hat unerwartet geantwortet.");
        }
    }

    /// <summary>Die Restzeit als «mm:ss» fuer den Dialog.</summary>
    public static string DescribeRemaining(TimeSpan remaining) =>
        remaining <= TimeSpan.Zero
            ? "00:00"
            : ((int)remaining.TotalMinutes).ToString("00", CultureInfo.InvariantCulture)
                + ":" + remaining.Seconds.ToString("00", CultureInfo.InvariantCulture);

    private static string Describe(string? error, string? description) => error switch
    {
        null or "" => "GitHub hat unerwartet geantwortet.",
        "unsupported_grant_type" => "Diese Anmeldeart ist nicht freigeschaltet.",
        "incorrect_client_credentials" => "Die Anwendung ist bei GitHub nicht bekannt.",
        _ => string.IsNullOrWhiteSpace(description) ? $"GitHub meldet «{error}»." : description
    };

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static TimeSpan Seconds(JsonElement root, string name, TimeSpan fallback) =>
        root.TryGetProperty(name, out var value) && value.TryGetInt32(out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : fallback;
}
