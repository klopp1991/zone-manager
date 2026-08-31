namespace SnapZones.Core.AppRules;

public static class AppRuleMatcher
{
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

        return AppCriteria.Matches(rule.ProcessPath, rule.WindowTitlePattern, rule.WindowClass, window);
    }

    private static int Specificity(AppRule rule) =>
        AppCriteria.Specificity(rule.ProcessPath, rule.WindowTitlePattern, rule.WindowClass);
}
