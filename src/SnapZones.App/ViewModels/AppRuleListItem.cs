using SnapZones.Core.AppRules;

namespace SnapZones.App.ViewModels;

/// <summary>
/// Eine Zuordnung, wie die Liste «Fenster zuordnen» sie zeigt: Programm, Ziel, Kurzbeschreibung und der
/// Hinweis, warum sie gerade nicht greift. Die Eintraege bleiben beim Aendern einer Zuordnung dieselben
/// Objekte und werden nur nachgefuehrt; sonst verloere das Textfeld im aufgeklappten Detail bei jedem
/// Tastendruck den Fokus.
/// </summary>
public sealed class AppRuleListItem : ViewModelBase
{
    private AppRule rule;
    private string? warning;
    private string targetLabel = string.Empty;
    private string subtitle = string.Empty;
    private bool isExpanded;

    public AppRuleListItem(AppRule rule, string? warning)
        : this(rule, warning, string.Empty, string.Empty)
    {
    }

    public AppRuleListItem(AppRule rule, string? warning, string targetLabel, string subtitle)
    {
        this.rule = rule ?? throw new ArgumentNullException(nameof(rule));
        this.warning = warning;
        this.targetLabel = targetLabel;
        this.subtitle = subtitle;
    }

    public Guid Id => rule.Id;

    public AppRule Rule
    {
        get => rule;
        private set
        {
            if (SetProperty(ref rule, value))
            {
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(ProcessFileName));
                OnPropertyChanged(nameof(ProcessPath));
                OnPropertyChanged(nameof(Event));
                OnPropertyChanged(nameof(IsEnabled));
            }
        }
    }

    /// <summary>Warum die Zuordnung gerade nichts bewirkt, oder null, wenn sie greift.</summary>
    public string? Warning
    {
        get => warning;
        private set
        {
            if (SetProperty(ref warning, value))
            {
                OnPropertyChanged(nameof(HasWarning));
                OnPropertyChanged(nameof(IsPaused));
                OnPropertyChanged(nameof(ActionLabel));
            }
        }
    }

    /// <summary>Die Zielzone, oder der Hinweis «Ziel fehlt – Zuordnung pausiert».</summary>
    public string TargetLabel
    {
        get => targetLabel;
        private set => SetProperty(ref targetLabel, value);
    }

    /// <summary>Zweite Zeile: Ereignis · Monitor › Layout · Eingrenzung.</summary>
    public string Subtitle
    {
        get => subtitle;
        private set => SetProperty(ref subtitle, value);
    }

    /// <summary>Ob das Detail unter der Zeile aufgeklappt ist. Es ist hoechstens eines offen.</summary>
    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (SetProperty(ref isExpanded, value))
            {
                OnPropertyChanged(nameof(ActionLabel));
            }
        }
    }

    public string DisplayName => rule.DisplayName;
    public string ProcessFileName => rule.ProcessFileName;
    public string ProcessPath => rule.ProcessPath;
    public AppRuleEvent Event => rule.Event;
    public bool IsEnabled => rule.IsEnabled;
    public bool HasWarning => warning is not null;

    /// <summary>Wahr, wenn das Ziel fehlt – nicht, wenn die Zuordnung nur abgeschaltet oder ohne Merkmal ist.</summary>
    public bool IsPaused => warning is not null && warning.Contains("fehlt", StringComparison.Ordinal);

    /// <summary>Beschriftung der Schaltflaeche rechts: Beheben bei fehlendem Ziel, sonst Bearbeiten, offen Schliessen.</summary>
    public string ActionLabel => isExpanded ? "Schliessen" : IsPaused ? "Beheben" : "Bearbeiten";

    internal void Update(AppRule replacement, string? replacementWarning, string replacementTarget, string replacementSubtitle)
    {
        Rule = replacement;
        Warning = replacementWarning;
        TargetLabel = replacementTarget;
        Subtitle = replacementSubtitle;
    }
}
