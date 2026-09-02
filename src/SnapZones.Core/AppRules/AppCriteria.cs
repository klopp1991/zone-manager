using System.Text.RegularExpressions;

namespace SnapZones.Core.AppRules;

/// <summary>
/// Der gemeinsame Vergleich von Programm, Fenstertitel und Fensterklasse. App-Regeln und Ausschlüsse
/// beschreiben ein Fenster nach denselben drei Merkmalen und müssen es deshalb auch gleich vergleichen;
/// zwei getrennte Vergleiche würden bei gleicher Eingabe unterschiedlich urteilen.
/// </summary>
public static class AppCriteria
{
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Ob überhaupt ein Merkmal genannt ist, an dem ein Fenster erkannt werden kann. Ohne jedes Merkmal
    /// würde der Vergleich auf sämtliche Fenster passen; das ist nie gewollt.
    /// </summary>
    public static bool HasCriteria(string? processPath, string? titlePattern, string? windowClass) =>
        !string.IsNullOrWhiteSpace(processPath) ||
        !string.IsNullOrWhiteSpace(titlePattern) ||
        !string.IsNullOrWhiteSpace(windowClass);

    public static bool Matches(
        string? processPath,
        string? titlePattern,
        string? windowClass,
        AppWindowIdentity window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (!HasCriteria(processPath, titlePattern, windowClass))
        {
            return false;
        }

        return ProcessMatches(processPath, window.ProcessPath) &&
            TitleMatches(titlePattern, window.WindowTitle) &&
            ClassMatches(windowClass, window.WindowClass);
    }

    /// <summary>
    /// Je mehr Merkmale genannt sind, desto enger ist die Beschreibung gefasst und desto eher ist sie
    /// gemeint, wenn mehrere gleichrangige Einträge auf dasselbe Fenster passen.
    /// </summary>
    public static int Specificity(string? processPath, string? titlePattern, string? windowClass) =>
        (string.IsNullOrWhiteSpace(processPath) ? 0 : 4) +
        (string.IsNullOrWhiteSpace(titlePattern) ? 0 : 2) +
        (string.IsNullOrWhiteSpace(windowClass) ? 0 : 1);

    private static bool ProcessMatches(string? configured, string actual)
    {
        // Das Programm ist ein Filter wie Titelmuster und Fensterklasse: leer heisst «egal welches
        // Programm». So laesst sich ein Eintrag allein ueber den Fenstertitel oder die Klasse stellen.
        if (string.IsNullOrWhiteSpace(configured))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(actual))
        {
            return false;
        }

        var expected = configured.Trim().Trim('"');
        var actualPath = actual.Trim().Trim('"');
        if (expected.IndexOfAny(['\\', '/', ':']) >= 0)
        {
            return string.Equals(expected, actualPath, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(expected, Path.GetFileName(actualPath), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TitleMatches(string? pattern, string title)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return true;
        }

        var trimmed = pattern.Trim();
        return trimmed.IndexOfAny(['*', '?']) >= 0
            ? WildcardMatches(trimmed, title)
            : title.Contains(trimmed, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ClassMatches(string? pattern, string windowClass)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return true;
        }

        var trimmed = pattern.Trim();
        return trimmed.IndexOfAny(['*', '?']) >= 0
            ? WildcardMatches(trimmed, windowClass)
            : string.Equals(trimmed, windowClass, StringComparison.OrdinalIgnoreCase);
    }

    private static bool WildcardMatches(string pattern, string value)
    {
        var expression = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(
            value,
            expression,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            PatternTimeout);
    }
}
