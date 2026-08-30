using System.Text.RegularExpressions;

namespace SnapZones.Core.Placement;

public static class PlacementRuleResolver
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    public static RuleResolution Resolve(
        WindowIdentity identity,
        string title,
        IReadOnlyList<WindowPlacementRule> rules)
    {
        var matching = rules
            .Where(rule => rule.IsEnabled &&
                           string.Equals(rule.ApplicationKey, identity.ApplicationKey, StringComparison.OrdinalIgnoreCase) &&
                           (rule.WindowClass is null || string.Equals(rule.WindowClass, identity.WindowClass, StringComparison.Ordinal)) &&
                           (rule.WindowKind is null || rule.WindowKind == identity.Kind) &&
                           MatchesTitle(rule.TitlePattern, title))
            .Select(rule => (Rule: rule, Specificity: Specificity(rule)))
            .ToArray();

        if (matching.Length == 0)
        {
            return new RuleResolution(null, false);
        }

        var maximum = matching.Max(item => item.Specificity);
        var mostSpecific = matching.Where(item => item.Specificity == maximum).ToArray();
        return mostSpecific.Length == 1
            ? new RuleResolution(mostSpecific[0].Rule, false)
            : new RuleResolution(null, true);
    }

    private static int Specificity(WindowPlacementRule rule) =>
        (rule.TitlePattern is not null ? 4 : 0) +
        (rule.WindowClass is not null ? 2 : 0) +
        (rule.WindowKind is not null ? 1 : 0);

    private static bool MatchesTitle(string? pattern, string title)
    {
        if (pattern is null)
        {
            return true;
        }

        var expression = "^(?:" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + ")$";
        try
        {
            return Regex.IsMatch(title, expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
