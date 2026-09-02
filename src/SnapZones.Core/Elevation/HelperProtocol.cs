using System.Globalization;

namespace SnapZones.Core.Elevation;

/// <summary>
/// Das Zwiegespräch zwischen dem Programm und dem Hilfsprogramm mit <c>uiAccess</c>.
///
/// Bewusst winzig und rein textbasiert: eine Zeile hin, eine Zeile zurück. Der Helfer darf ausschliesslich
/// Fenster verschieben. Er nimmt keine Pfade entgegen, führt nichts aus, liest und schreibt keine Dateien
/// und kennt keine Sonderfälle. Alles, was er kann, steht in diesen wenigen Zeilen — genau das macht ihn
/// prüfbar, denn er läuft mit dem Recht, auch höher berechtigte Fenster anzufassen.
/// </summary>
public static class HelperProtocol
{
    /// <summary>Erhöht sich, sobald sich die Bedeutung einer Nachricht ändert.</summary>
    public const int Version = 1;

    public const string PlaceVerb = "PLACE";
    public const string PingVerb = "PING";
    public const string SuccessReply = "OK";
    public const string FailurePrefix = "FAIL";
    public const string PongPrefix = "PONG";

    /// <summary>
    /// Grenze für Bildschirmkoordinaten. Weit jenseits jeder denkbaren Monitoranordnung, aber eng genug,
    /// dass ein Zahlenüberlauf im Empfänger ausgeschlossen ist.
    /// </summary>
    public const int CoordinateLimit = 1_000_000;

    public static string BuildPing() => PingVerb;

    public static string BuildPong() =>
        string.Create(CultureInfo.InvariantCulture, $"{PongPrefix} {Version}");

    public static string BuildPlace(nint windowHandle, int x, int y, int width, int height) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{PlaceVerb} {(long)windowHandle} {x} {y} {width} {height}");

    public static string BuildFailure(string reason) =>
        $"{FailurePrefix} {Sanitize(reason)}";

    /// <summary>
    /// Zerlegt eine empfangene Zeile. Alles, was nicht genau der erwarteten Form entspricht, gilt als
    /// ungültig — es gibt keine Nachsicht und keine Vorgabewerte. Ein Empfänger mit erhöhten Rechten darf
    /// nichts erraten.
    /// </summary>
    public static bool TryParseRequest(string? line, out HelperRequest request)
    {
        request = default;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1 && string.Equals(parts[0], PingVerb, StringComparison.Ordinal))
        {
            request = new HelperRequest(HelperVerb.Ping, 0, 0, 0, 0, 0);
            return true;
        }

        if (parts.Length != 6 || !string.Equals(parts[0], PlaceVerb, StringComparison.Ordinal))
        {
            return false;
        }

        if (!long.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var handle) ||
            handle == 0 ||
            !TryParseCoordinate(parts[2], out var x) ||
            !TryParseCoordinate(parts[3], out var y) ||
            !TryParseCoordinate(parts[4], out var width) ||
            !TryParseCoordinate(parts[5], out var height) ||
            width < 1 ||
            height < 1)
        {
            return false;
        }

        request = new HelperRequest(HelperVerb.Place, (nint)handle, x, y, width, height);
        return true;
    }

    public static bool IsSuccess(string? reply) =>
        string.Equals(reply?.Trim(), SuccessReply, StringComparison.Ordinal);

    /// <summary>Liest die Versionsnummer aus einer Antwort auf <c>PING</c>.</summary>
    public static bool TryParsePong(string? reply, out int version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(reply))
        {
            return false;
        }

        var parts = reply.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 &&
            string.Equals(parts[0], PongPrefix, StringComparison.Ordinal) &&
            int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }

    /// <summary>Die Begründung eines Fehlschlags, oder ein leerer Text.</summary>
    public static string ReadFailureReason(string? reply)
    {
        var trimmed = reply?.Trim() ?? string.Empty;
        return trimmed.StartsWith(FailurePrefix + " ", StringComparison.Ordinal)
            ? trimmed[(FailurePrefix.Length + 1)..]
            : string.Empty;
    }

    private static bool TryParseCoordinate(string text, out int value) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value) &&
        Math.Abs((long)value) <= CoordinateLimit;

    /// <summary>Eine Begründung darf die Zeilenstruktur nicht sprengen.</summary>
    private static string Sanitize(string reason)
    {
        var cleaned = (reason ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        return cleaned.Length == 0
            ? "Unbekannter Grund"
            : cleaned.Length > 200 ? cleaned[..200] : cleaned;
    }
}

public enum HelperVerb
{
    Ping,
    Place
}

public readonly record struct HelperRequest(
    HelperVerb Verb,
    nint WindowHandle,
    int X,
    int Y,
    int Width,
    int Height);
