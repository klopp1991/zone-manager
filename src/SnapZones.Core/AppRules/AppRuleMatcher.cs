using System.Text.RegularExpressions;

namespace SnapZones.Core.AppRules;

public static class AppRuleMatcher
{
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(100);

    public static AppRule? Resolve(
        IEnumerable<AppRule> rules,
        AppRuleEvent eventType,
        AppWindowIdentity window)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(window);

        return rules
            .Where(rule => rule.IsEnabled && rule.Event == eventType && Matches(rule, window))
            .OrderByDescending(rule => rule.Priority)
            .ThenByDescending(Specificity)
            .ThenBy(rule => rule.Id)
            .FirstOrDefault();
    }

    public static bool Matches(AppRule rule, AppWindowIdentity window)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(window);

        // Ohne jedes Merkmal wuerde die Regel auf saemtliche Fenster passen. Das ist nie gewollt und
        // waere obendrein gefaehrlich, weil eine halb ausgefuellte Regel sofort alles verschieben wuerde.
        if (!rule.HasCriteria)
        {
            return false;
        }

        return OptionalProcessMatches(rule.ProcessPath, window.ProcessPath) &&
            OptionalTitleMatches(rule.WindowTitlePattern, window.WindowTitle) &&
            OptionalClassMatches(rule.WindowClass, window.WindowClass);
    }

    private static bool OptionalProcessMatches(string configured, string actual)
    {
        // Das Programm ist ein Filter wie Titelmuster und Fensterklasse: leer heisst «egal welches
        // Programm». So laesst sich eine Regel allein ueber den Fenstertitel oder die Klasse stellen.
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

    private static bool OptionalTitleMatches(string? pattern, string title)
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

    private static bool OptionalClassMatches(string? pattern, string windowClass)
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

    /// <summary>
    /// Je mehr Merkmale eine Regel nennt, desto enger ist sie gefasst und desto eher ist sie gemeint,
    /// wenn mehrere Regeln bei gleicher Priorität auf dasselbe Fenster passen.
    /// </summary>
    private static int Specificity(AppRule rule) =>
        (string.IsNullOrWhiteSpace(rule.ProcessPath) ? 0 : 4) +
        (string.IsNullOrWhiteSpace(rule.WindowTitlePattern) ? 0 : 2) +
        (string.IsNullOrWhiteSpace(rule.WindowClass) ? 0 : 1);
}
