using SnapZones.Core.AppRules;

namespace SnapZones.App.ViewModels;

/// <summary>Eine Regel samt dem Hinweis, warum sie gerade nicht greift, fuer die Regelliste.</summary>
public sealed record AppRuleListItem(AppRule Rule, string? Warning)
{
    public string DisplayName => Rule.DisplayName;
    public string ProcessFileName => Rule.ProcessFileName;
    public string ProcessPath => Rule.ProcessPath;
    public AppRuleEvent Event => Rule.Event;
    public bool HasWarning => Warning is not null;
}
