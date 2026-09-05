using SnapZones.Core.AppRules;

namespace SnapZones.App.ViewModels;

/// <summary>
/// Ein Eintrag der Liste «In Ruhe lassen». Wie <see cref="AppRuleListItem"/> bleibt das Objekt beim Aendern
/// dasselbe und wird nur nachgefuehrt, damit das aufgeklappte Detail beim Tippen stehen bleibt.
/// </summary>
public sealed class AppExclusionListItem : ViewModelBase
{
    private AppExclusion exclusion;
    private bool isExpanded;

    public AppExclusionListItem(AppExclusion exclusion)
    {
        this.exclusion = exclusion ?? throw new ArgumentNullException(nameof(exclusion));
    }

    public Guid Id => exclusion.Id;

    public AppExclusion Exclusion
    {
        get => exclusion;
        private set
        {
            if (SetProperty(ref exclusion, value))
            {
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(ProcessPath));
                OnPropertyChanged(nameof(Subtitle));
                OnPropertyChanged(nameof(IsEnabled));
            }
        }
    }

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

    public string DisplayName => exclusion.DisplayName;
    public string ProcessPath => exclusion.ProcessPath;
    public bool IsEnabled => exclusion.IsEnabled;

    /// <summary>«Alle Fenster», oder welche Fenster des Programms gemeint sind.</summary>
    public string Subtitle
    {
        get
        {
            var parts = new List<string>(2);
            if (!string.IsNullOrWhiteSpace(exclusion.WindowTitlePattern))
            {
                parts.Add($"Titel enthält «{exclusion.WindowTitlePattern}»");
            }

            if (!string.IsNullOrWhiteSpace(exclusion.WindowClass))
            {
                parts.Add($"Fensterklasse {exclusion.WindowClass}");
            }

            var text = parts.Count == 0 ? "Alle Fenster" : string.Join(" · ", parts);
            return exclusion.IsEnabled ? text : $"{text} · ausgeschaltet";
        }
    }

    public string ActionLabel => isExpanded ? "Schliessen" : "Eingrenzen …";

    internal void Update(AppExclusion replacement) => Exclusion = replacement;
}
