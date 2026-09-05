using System.Collections.ObjectModel;
using SnapZones.Core.AppRules;
using SnapZones.Core.Models;

namespace SnapZones.App.ViewModels;

public sealed class AppRuleEditorViewModel : ViewModelBase
{
    private readonly ObservableCollection<MonitorLayout> targetLayouts = [];
    private AppRule? selectedRule;
    private string processPath = string.Empty;
    private string windowTitlePattern = string.Empty;
    private string windowClass = string.Empty;
    private AppRuleEvent selectedEvent = AppRuleEvent.WindowCreated;
    private int delayMilliseconds;
    private int retryCount;
    private int priority = 50;
    private bool isEnabled = true;
    private MonitorLayout? selectedTargetLayout;
    private ZoneDefinition? selectedTargetZone;
    private Guid editedRuleId;
    private Guid targetLayoutId;
    private Guid targetZoneId;
    private string targetStatus = "Ziel auswählen";
    private Guid? expandedRuleId;
    private bool loading;

    public AppRuleEditorViewModel(
        IReadOnlyList<AppRule> rules,
        IReadOnlyList<MonitorLayout> layouts)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(layouts);
        Rules = new ObservableCollection<AppRule>(rules);
        Rules.CollectionChanged += (_, _) => SyncRuleItems();
        RefreshTargetCollection(layouts);
        if (Rules.Count > 0)
        {
            SelectedRule = Rules[0];
        }
        else
        {
            ResetEditor();
        }
    }

    public event Action<IReadOnlyList<AppRule>>? RulesChanged;

    public ObservableCollection<AppRule> Rules { get; }

    /// <summary>
    /// Die Zuordnungen, wie die Liste sie zeigt: jede mit Ziel, Kurzbeschreibung und dem Hinweis, warum sie
    /// gerade nicht greift. Die Eintraege werden in place nachgefuehrt, nicht neu erzeugt.
    /// </summary>
    public ObservableCollection<AppRuleListItem> RuleItems { get; } = [];

    public AppRuleListItem? SelectedRuleItem
    {
        get => RuleItems.FirstOrDefault(item => item.Rule.Id == selectedRule?.Id);
        set
        {
            // Ein Neuaufbau der Liste meldet kurz null; die Auswahl bleibt dann beim bisherigen Eintrag.
            if (value is not null)
            {
                SelectedRule = value.Rule;
            }
        }
    }

    public IReadOnlyList<AppRuleEvent> Events { get; } = Enum.GetValues<AppRuleEvent>();
    public ObservableCollection<ZoneDefinition> TargetZones { get; } = [];
    public IReadOnlyList<MonitorLayout> TargetLayouts => targetLayouts;

    /// <summary>Wie viele Zuordnungen wegen eines fehlenden Ziels pausiert sind.</summary>
    public int PausedCount => RuleItems.Count(item => item.IsPaused);

    /// <summary>Der aufgeklappte Eintrag; er ist zugleich der bearbeitete.</summary>
    public Guid? ExpandedRuleId
    {
        get => expandedRuleId;
        private set
        {
            if (SetProperty(ref expandedRuleId, value))
            {
                foreach (var item in RuleItems)
                {
                    item.IsExpanded = item.Id == value;
                }
            }
        }
    }

    public AppRule? SelectedRule
    {
        get => selectedRule;
        set
        {
            if (!SetProperty(ref selectedRule, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanDelete));
            OnPropertyChanged(nameof(SelectedRuleItem));
            if (value is not null)
            {
                Load(value);
            }
        }
    }

    public string ProcessPath
    {
        get => processPath;
        set
        {
            SetEditorProperty(ref processPath, value ?? string.Empty);
            OnPropertyChanged(nameof(CriteriaStatus));
        }
    }

    public string WindowTitlePattern
    {
        get => windowTitlePattern;
        set
        {
            SetEditorProperty(ref windowTitlePattern, value ?? string.Empty);
            OnPropertyChanged(nameof(CriteriaStatus));
        }
    }

    public string WindowClass
    {
        get => windowClass;
        set
        {
            SetEditorProperty(ref windowClass, value ?? string.Empty);
            OnPropertyChanged(nameof(CriteriaStatus));
        }
    }

    /// <summary>
    /// Hinweis darüber, ob die Zuordnung überhaupt ein Merkmal nennt. Programm, Titelmuster und
    /// Fensterklasse sind gleichrangig; jedes einzelne genügt. Wer vom Pfad auf das Titelmuster
    /// umstellt, löscht deshalb einfach den Pfad – die Zuordnung bleibt bestehen und wird gespeichert.
    /// </summary>
    public string CriteriaStatus => HasCriteria
        ? string.Empty
        : "Diese Zuordnung greift noch nicht: Trage mindestens eines der drei Merkmale ein – " +
            "Programm, Titel oder Fensterklasse.";

    /// <summary>Ob mindestens eines der drei Erkennungsmerkmale ausgefüllt ist.</summary>
    public bool HasCriteria =>
        processPath.Trim().Length > 0 ||
        windowTitlePattern.Trim().Length > 0 ||
        windowClass.Trim().Length > 0;

    public AppRuleEvent SelectedEvent
    {
        get => selectedEvent;
        set
        {
            SetEditorProperty(ref selectedEvent, value);
            OnPropertyChanged(nameof(SelectedEventDescription));
        }
    }

    /// <summary>Ausformulierte Erklärung des gewählten Ereignisses für die Oberfläche.</summary>
    public string SelectedEventDescription => DescribeEvent(selectedEvent);

    /// <summary>Erklärt ein Ereignis in wenigen Sätzen. Öffentlich, damit die Formulierung prüfbar bleibt.</summary>
    public static string DescribeEvent(AppRuleEvent value) => value switch
    {
        AppRuleEvent.WindowCreated =>
            "Greift genau einmal, sobald das Programm ein neues Fenster öffnet – beim Programmstart oder wenn ein weiteres " +
            "Fenster aufgeht. Danach kannst du das Fenster frei verschieben; es wird nicht zurückgeholt.",
        AppRuleEvent.WindowFocused =>
            "Greift jedes Mal, wenn du zu einem passenden Fenster wechselst, es also den Fokus erhält. Das Fenster kehrt damit " +
            "immer wieder in seine Zone zurück – sinnvoll für Fenster, die dauerhaft am selben Platz stehen sollen.",
        AppRuleEvent.LayoutActivated =>
            "Greift nicht beim Fenster, sondern beim Layoutwechsel: Aktivierst du das Ziellayout, werden alle bereits offenen " +
            "passenden Fenster auf einen Schlag neu angeordnet. Später geöffnete Fenster bleiben unberührt.",
        _ => string.Empty
    };

    /// <summary>Das Ereignis in zwei Worten fuer die Listenzeile.</summary>
    public static string ShortEvent(AppRuleEvent value) => value switch
    {
        AppRuleEvent.WindowFocused => "Beim Fokus",
        AppRuleEvent.LayoutActivated => "Bei Layoutwechsel",
        _ => "Beim Öffnen"
    };

    public int DelayMilliseconds
    {
        get => delayMilliseconds;
        set => SetEditorProperty(ref delayMilliseconds, Math.Clamp(value, 0, 30000));
    }

    public int RetryCount
    {
        get => retryCount;
        set => SetEditorProperty(ref retryCount, Math.Clamp(value, 0, 3));
    }

    public int Priority
    {
        get => priority;
        set => SetEditorProperty(ref priority, Math.Clamp(value, 0, 100));
    }

    public bool IsEnabled
    {
        get => isEnabled;
        set => SetEditorProperty(ref isEnabled, value);
    }

    public MonitorLayout? SelectedTargetLayout
    {
        get => selectedTargetLayout;
        set
        {
            if (!SetProperty(ref selectedTargetLayout, value))
            {
                return;
            }

            if (!loading && value is not null)
            {
                targetLayoutId = value.Id;
                targetZoneId = value.Zones.FirstOrDefault()?.Id ?? Guid.Empty;
            }

            RefreshZones();
            if (!loading)
            {
                TryPersist();
            }
        }
    }

    public ZoneDefinition? SelectedTargetZone
    {
        get => selectedTargetZone;
        set
        {
            if (!SetProperty(ref selectedTargetZone, value))
            {
                return;
            }

            if (!loading && value is not null)
            {
                targetZoneId = value.Id;
                UpdateTargetStatus();
                TryPersist();
            }
        }
    }

    public string TargetStatus
    {
        get => targetStatus;
        private set
        {
            if (SetProperty(ref targetStatus, value))
            {
                OnPropertyChanged(nameof(IsTargetMissing));
                OnPropertyChanged(nameof(MissingTargetExplanation));
            }
        }
    }

    /// <summary>Ob die bearbeitete Zuordnung auf ein Layout oder eine Zone zeigt, die es nicht mehr gibt.</summary>
    public bool IsTargetMissing =>
        targetLayoutId != Guid.Empty && targetZoneId != Guid.Empty &&
        (selectedTargetLayout is null || selectedTargetZone is null);

    /// <summary>Was fehlt und wie es weitergeht, fuer die Hinweisbox im Detail.</summary>
    public string MissingTargetExplanation
    {
        get
        {
            if (!IsTargetMissing)
            {
                return string.Empty;
            }

            if (selectedTargetLayout is null)
            {
                return "Das Ziellayout gibt es nicht mehr. Wähle unten ein Layout und eine Zone – die Zuordnung läuft danach wieder.";
            }

            return $"Die Zielzone gibt es im Layout «{selectedTargetLayout.Name}» nicht mehr. Wähle unten eine neue Zone – die Zuordnung läuft danach wieder.";
        }
    }

    public bool CanDelete => SelectedRule is not null;

    /// <summary>
    /// Legt eine neue Zuordnung an und trägt sie sofort in die Liste ein. Der Eintrag entsteht bewusst
    /// vor der ersten Eingabe: sonst hätte der Knopf bei einer bereits vorhandenen Zuordnung keine
    /// sichtbare Wirkung, und ein angefangener Entwurf ginge beim nächsten Klick in der Liste
    /// verloren. Die Zuordnung bleibt wirkungslos, solange sie kein Merkmal nennt.
    /// </summary>
    public void AddRule()
    {
        ResetEditor();
        TryPersist();
        ExpandedRuleId = editedRuleId;
    }

    /// <summary>
    /// Legt eine vollstaendige Zuordnung an, wie sie der Dialog «Fenster zuordnen» liefert: Programmname,
    /// Ereignis und Ziel. Sie greift sofort.
    /// </summary>
    public AppRule AddRule(string processName, AppRuleEvent ruleEvent, Guid layoutId, Guid zoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);
        ResetEditor();
        loading = true;
        try
        {
            processPath = processName.Trim();
            selectedEvent = ruleEvent;
            targetLayoutId = layoutId;
            targetZoneId = zoneId;
            RaiseEditorProperties();
            ResolveTargetSelection();
        }
        finally
        {
            loading = false;
        }

        TryPersist();
        return Rules.First(rule => rule.Id == editedRuleId);
    }

    /// <summary>Klappt das Detail eines Eintrags auf oder zu und macht ihn zum bearbeiteten.</summary>
    public void ToggleExpanded(AppRuleListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (ExpandedRuleId == item.Id)
        {
            ExpandedRuleId = null;
            return;
        }

        SelectedRule = item.Rule;
        ExpandedRuleId = item.Id;
    }

    public void CollapseAll() => ExpandedRuleId = null;

    /// <summary>Schaltet eine Zuordnung direkt aus der Liste ein oder aus, ohne sie aufzuklappen.</summary>
    public void SetEnabled(AppRuleListItem item, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(item);
        var index = Rules.ToList().FindIndex(rule => rule.Id == item.Id);
        if (index < 0 || Rules[index].IsEnabled == enabled)
        {
            return;
        }

        var replacement = Rules[index] with { IsEnabled = enabled };
        Rules[index] = replacement;
        if (selectedRule?.Id == replacement.Id)
        {
            selectedRule = replacement;
            isEnabled = enabled;
            OnPropertyChanged(nameof(SelectedRule));
            OnPropertyChanged(nameof(IsEnabled));
        }

        RulesChanged?.Invoke(Rules.ToArray());
    }

    /// <summary>
    /// Entfernt eine Zuordnung und liefert ihre Position, damit «Rueckgaengig» sie an dieselbe Stelle
    /// zurueckschreiben kann. Das Loeschen ist sofort gespeichert.
    /// </summary>
    public int RemoveRule(AppRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var index = Rules.ToList().FindIndex(candidate => candidate.Id == rule.Id);
        if (index < 0)
        {
            return -1;
        }

        if (ExpandedRuleId == rule.Id)
        {
            ExpandedRuleId = null;
        }

        Rules.RemoveAt(index);
        RulesChanged?.Invoke(Rules.ToArray());
        if (Rules.Count == 0)
        {
            ResetEditor();
        }
        else if (selectedRule?.Id == rule.Id)
        {
            SelectedRule = Rules[Math.Min(index, Rules.Count - 1)];
        }

        return index;
    }

    /// <summary>Schreibt eine entfernte Zuordnung zurueck – der Rueckweg aus dem Toast.</summary>
    public void RestoreRule(AppRule rule, int index)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (Rules.Any(candidate => candidate.Id == rule.Id))
        {
            return;
        }

        Rules.Insert(Math.Clamp(index, 0, Rules.Count), rule);
        SelectedRule = rule;
        RulesChanged?.Invoke(Rules.ToArray());
    }

    public void DeleteSelectedRule()
    {
        if (SelectedRule is { } selected)
        {
            RemoveRule(selected);
        }
    }

    public void Refresh(
        IReadOnlyList<AppRule> rules,
        IReadOnlyList<MonitorLayout> layouts)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var selectedId = SelectedRule?.Id ?? editedRuleId;
        loading = true;
        try
        {
            Rules.Clear();
            foreach (var rule in rules)
            {
                Rules.Add(rule);
            }

            RefreshTargetCollection(layouts);
        }
        finally
        {
            loading = false;
        }

        selectedRule = null;
        OnPropertyChanged(nameof(SelectedRule));
        SelectedRule = Rules.FirstOrDefault(rule => rule.Id == selectedId) ?? Rules.FirstOrDefault();
        if (SelectedRule is null)
        {
            ResetEditor();
        }

        if (ExpandedRuleId is { } expanded && Rules.All(rule => rule.Id != expanded))
        {
            ExpandedRuleId = null;
        }
    }

    public void RefreshTargets(IReadOnlyList<MonitorLayout> layouts)
    {
        RefreshTargetCollection(layouts);
        ResolveTargetSelection();
    }

    /// <summary>Warum eine Zuordnung gerade nichts bewirkt, oder null, wenn sie greift.</summary>
    public string? DescribeProblem(AppRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (!rule.IsEnabled)
        {
            return "Abgeschaltet";
        }

        if (!rule.HasCriteria)
        {
            return "Kein Merkmal – Zuordnung wirkungslos";
        }

        var layout = targetLayouts.FirstOrDefault(candidate => candidate.Id == rule.TargetLayoutId);
        if (layout is null)
        {
            return "Ziellayout fehlt – Zuordnung pausiert";
        }

        if (layout.Zones.All(zone => zone.Id != rule.TargetZoneId))
        {
            return "Zielzone fehlt – Zuordnung pausiert";
        }

        return null;
    }

    /// <summary>Die Zielzone einer Zuordnung mit Namen, oder der Hinweis auf das fehlende Ziel.</summary>
    public string DescribeTarget(AppRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var layout = targetLayouts.FirstOrDefault(candidate => candidate.Id == rule.TargetLayoutId);
        var zone = layout?.Zones.FirstOrDefault(candidate => candidate.Id == rule.TargetZoneId);
        return zone?.Name ?? "Ziel fehlt – Zuordnung pausiert";
    }

    /// <summary>Zweite Zeile der Listenzeile: Ereignis · Monitor › Layout · Eingrenzung.</summary>
    public string DescribeSubtitle(AppRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var parts = new List<string> { ShortEvent(rule.Event) };
        var layout = targetLayouts.FirstOrDefault(candidate => candidate.Id == rule.TargetLayoutId);
        if (layout is null)
        {
            parts.Add("das Ziellayout gibt es nicht mehr");
        }
        else
        {
            parts.Add(string.IsNullOrWhiteSpace(layout.UserFacingMonitorName)
                ? layout.Name
                : $"{layout.UserFacingMonitorName} › {layout.Name}");
            if (layout.Zones.All(zone => zone.Id != rule.TargetZoneId))
            {
                parts.Add("die Zielzone gibt es nicht mehr");
            }
        }

        if (!string.IsNullOrWhiteSpace(rule.WindowTitlePattern))
        {
            parts.Add($"Titel «{rule.WindowTitlePattern}»");
        }

        if (!string.IsNullOrWhiteSpace(rule.WindowClass))
        {
            parts.Add($"Fensterklasse {rule.WindowClass}");
        }

        if (!rule.IsEnabled)
        {
            parts.Add("ausgeschaltet");
        }

        return string.Join(" · ", parts);
    }

    private void ResetEditor()
    {
        loading = true;
        try
        {
            selectedRule = null;
            OnPropertyChanged(nameof(SelectedRule));
            OnPropertyChanged(nameof(SelectedRuleItem));
            OnPropertyChanged(nameof(CanDelete));
            editedRuleId = Guid.NewGuid();
            processPath = string.Empty;
            windowTitlePattern = string.Empty;
            windowClass = string.Empty;
            selectedEvent = AppRuleEvent.WindowCreated;
            delayMilliseconds = 0;
            retryCount = 0;
            priority = 50;
            isEnabled = true;
            targetLayoutId = targetLayouts.FirstOrDefault()?.Id ?? Guid.Empty;
            targetZoneId = targetLayouts.FirstOrDefault()?.Zones.FirstOrDefault()?.Id ?? Guid.Empty;
            RaiseEditorProperties();
            ResolveTargetSelection();
        }
        finally
        {
            loading = false;
        }
    }

    private void Load(AppRule rule)
    {
        loading = true;
        try
        {
            editedRuleId = rule.Id;
            processPath = rule.ProcessPath;
            windowTitlePattern = rule.WindowTitlePattern ?? string.Empty;
            windowClass = rule.WindowClass ?? string.Empty;
            selectedEvent = rule.Event;
            delayMilliseconds = rule.DelayMilliseconds;
            retryCount = rule.RetryCount;
            priority = rule.Priority;
            isEnabled = rule.IsEnabled;
            targetLayoutId = rule.TargetLayoutId;
            targetZoneId = rule.TargetZoneId;
            RaiseEditorProperties();
            ResolveTargetSelection();
        }
        finally
        {
            loading = false;
        }
    }

    private void RefreshTargetCollection(IReadOnlyList<MonitorLayout> layouts)
    {
        targetLayouts.Clear();
        foreach (var layout in layouts)
        {
            targetLayouts.Add(layout);
        }

        OnPropertyChanged(nameof(TargetLayouts));
        SyncRuleItems();
    }

    /// <summary>
    /// Fuehrt die Listeneintraege nach: bestehende Eintraege bleiben dieselben Objekte, neue kommen an ihre
    /// Position, verschwundene gehen. So ueberlebt das aufgeklappte Detail jede Speicherung.
    /// </summary>
    private void SyncRuleItems()
    {
        var byId = RuleItems.ToDictionary(item => item.Id);
        for (var index = 0; index < Rules.Count; index++)
        {
            var rule = Rules[index];
            var warning = DescribeProblem(rule);
            var target = DescribeTarget(rule);
            var subtitle = DescribeSubtitle(rule);
            if (byId.TryGetValue(rule.Id, out var existing))
            {
                existing.Update(rule, warning, target, subtitle);
                var currentIndex = RuleItems.IndexOf(existing);
                if (currentIndex != index && index < RuleItems.Count)
                {
                    RuleItems.Move(currentIndex, index);
                }
            }
            else
            {
                var item = new AppRuleListItem(rule, warning, target, subtitle) { IsExpanded = rule.Id == expandedRuleId };
                RuleItems.Insert(Math.Min(index, RuleItems.Count), item);
            }
        }

        var ruleIds = Rules.Select(rule => rule.Id).ToHashSet();
        foreach (var stale in RuleItems.Where(item => !ruleIds.Contains(item.Id)).ToArray())
        {
            RuleItems.Remove(stale);
        }

        OnPropertyChanged(nameof(SelectedRuleItem));
        OnPropertyChanged(nameof(PausedCount));
    }

    private void ResolveTargetSelection()
    {
        loading = true;
        try
        {
            selectedTargetLayout = targetLayouts.FirstOrDefault(layout => layout.Id == targetLayoutId);
            OnPropertyChanged(nameof(SelectedTargetLayout));
            RefreshZones();
        }
        finally
        {
            loading = false;
        }
    }

    private void RefreshZones()
    {
        TargetZones.Clear();
        if (selectedTargetLayout is not null)
        {
            foreach (var zone in selectedTargetLayout.Zones)
            {
                TargetZones.Add(zone);
            }
        }

        selectedTargetZone = TargetZones.FirstOrDefault(zone => zone.Id == targetZoneId);
        OnPropertyChanged(nameof(SelectedTargetZone));
        UpdateTargetStatus();
    }

    private void UpdateTargetStatus()
    {
        if (targetLayoutId == Guid.Empty || targetZoneId == Guid.Empty)
        {
            TargetStatus = "Ziel auswählen";
            return;
        }

        if (selectedTargetLayout is null || selectedTargetZone is null)
        {
            TargetStatus = "Ziel nicht verfügbar – Zuordnung pausiert";
            return;
        }

        TargetStatus = $"Ziel: {selectedTargetLayout.Name} / {selectedTargetZone.Name}";
    }

    private void SetEditorProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref field, value, propertyName) || loading)
        {
            return;
        }

        TryPersist();
    }

    private void TryPersist()
    {
        var normalizedProcess = ProcessPath.Trim().Trim('"');
        // Das Programm ist kein Pflichtfeld: eine Zuordnung darf sich allein auf den Titel oder die
        // Fensterklasse stuetzen. Nur so laesst sich ein einmal gesetzter Pfad wieder loeschen.
        if (normalizedProcess.Length > 1024 ||
            WindowTitlePattern.Trim().Length > 512 ||
            WindowClass.Trim().Length > 256 ||
            targetLayoutId == Guid.Empty ||
            targetZoneId == Guid.Empty)
        {
            return;
        }

        var rule = new AppRule(
            editedRuleId,
            normalizedProcess,
            Optional(WindowTitlePattern),
            Optional(WindowClass),
            SelectedEvent,
            DelayMilliseconds,
            RetryCount,
            Priority,
            IsEnabled,
            targetLayoutId,
            targetZoneId);
        var index = Rules.ToList().FindIndex(candidate => candidate.Id == editedRuleId);
        if (index >= 0 && Rules[index] == rule)
        {
            return;
        }

        if (index >= 0)
        {
            Rules[index] = rule;
        }
        else
        {
            Rules.Add(rule);
        }

        selectedRule = rule;
        OnPropertyChanged(nameof(SelectedRule));
        OnPropertyChanged(nameof(SelectedRuleItem));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CriteriaStatus));
        RulesChanged?.Invoke(Rules.ToArray());
    }

    private void RaiseEditorProperties()
    {
        OnPropertyChanged(nameof(ProcessPath));
        OnPropertyChanged(nameof(WindowTitlePattern));
        OnPropertyChanged(nameof(WindowClass));
        OnPropertyChanged(nameof(CriteriaStatus));
        OnPropertyChanged(nameof(SelectedEvent));
        OnPropertyChanged(nameof(SelectedEventDescription));
        OnPropertyChanged(nameof(DelayMilliseconds));
        OnPropertyChanged(nameof(RetryCount));
        OnPropertyChanged(nameof(Priority));
        OnPropertyChanged(nameof(IsEnabled));
    }

    private static string? Optional(string value)
    {
        var normalized = value.Trim();
        return normalized.Length == 0 ? null : normalized;
    }
}
