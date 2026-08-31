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
    private bool loading;

    public AppRuleEditorViewModel(
        IReadOnlyList<AppRule> rules,
        IReadOnlyList<MonitorLayout> layouts)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(layouts);
        Rules = new ObservableCollection<AppRule>(rules);
        RefreshTargetCollection(layouts);
        if (Rules.Count > 0)
        {
            SelectedRule = Rules[0];
        }
        else
        {
            AddRule();
        }
    }

    public event Action<IReadOnlyList<AppRule>>? RulesChanged;

    public ObservableCollection<AppRule> Rules { get; }
    public IReadOnlyList<AppRuleEvent> Events { get; } = Enum.GetValues<AppRuleEvent>();
    public ObservableCollection<ZoneDefinition> TargetZones { get; } = [];
    public IReadOnlyList<MonitorLayout> TargetLayouts => targetLayouts;

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
            if (value is not null)
            {
                Load(value);
            }
        }
    }

    public string ProcessPath
    {
        get => processPath;
        set => SetEditorProperty(ref processPath, value ?? string.Empty);
    }

    public string WindowTitlePattern
    {
        get => windowTitlePattern;
        set => SetEditorProperty(ref windowTitlePattern, value ?? string.Empty);
    }

    public string WindowClass
    {
        get => windowClass;
        set => SetEditorProperty(ref windowClass, value ?? string.Empty);
    }

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
        private set => SetProperty(ref targetStatus, value);
    }

    public bool CanDelete => SelectedRule is not null;

    public void AddRule()
    {
        loading = true;
        try
        {
            selectedRule = null;
            OnPropertyChanged(nameof(SelectedRule));
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

    public void DeleteSelectedRule()
    {
        if (SelectedRule is not { } selected)
        {
            return;
        }

        var index = Rules.IndexOf(selected);
        Rules.Remove(selected);
        RulesChanged?.Invoke(Rules.ToArray());
        if (Rules.Count == 0)
        {
            AddRule();
            return;
        }

        SelectedRule = Rules[Math.Min(index, Rules.Count - 1)];
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
            AddRule();
        }
    }

    public void RefreshTargets(IReadOnlyList<MonitorLayout> layouts)
    {
        RefreshTargetCollection(layouts);
        ResolveTargetSelection();
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
            TargetStatus = "Ziel nicht verfügbar – Regel pausiert";
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
        if (string.IsNullOrWhiteSpace(normalizedProcess) ||
            normalizedProcess.Length > 1024 ||
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
        OnPropertyChanged(nameof(CanDelete));
        RulesChanged?.Invoke(Rules.ToArray());
    }

    private void RaiseEditorProperties()
    {
        OnPropertyChanged(nameof(ProcessPath));
        OnPropertyChanged(nameof(WindowTitlePattern));
        OnPropertyChanged(nameof(WindowClass));
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
