using System.Collections.ObjectModel;
using SnapZones.Core.AppRules;

namespace SnapZones.App.ViewModels;

/// <summary>
/// Verwaltet die Liste der Fenster, die in Ruhe gelassen werden. Ein Ausschluss ist bewusst schlanker als
/// eine Zuordnung: er hat kein Ziel, kein Ereignis und keine Priorität, weil er nichts anordnet, sondern
/// nur bewirkt, dass die Anwendung ein Fenster in Ruhe lässt.
/// </summary>
public sealed class AppExclusionEditorViewModel : ViewModelBase
{
    private AppExclusion? selectedExclusion;
    private string processPath = string.Empty;
    private string windowTitlePattern = string.Empty;
    private string windowClass = string.Empty;
    private bool isEnabled = true;
    private Guid editedExclusionId;
    private Guid? expandedExclusionId;
    private bool loading;

    public AppExclusionEditorViewModel(IReadOnlyList<AppExclusion> exclusions)
    {
        ArgumentNullException.ThrowIfNull(exclusions);
        Exclusions = new ObservableCollection<AppExclusion>(exclusions);
        Exclusions.CollectionChanged += (_, _) => SyncItems();
        SyncItems();
        if (Exclusions.Count > 0)
        {
            SelectedExclusion = Exclusions[0];
        }
        else
        {
            ResetEditor();
        }
    }

    public event Action<IReadOnlyList<AppExclusion>>? ExclusionsChanged;

    public ObservableCollection<AppExclusion> Exclusions { get; }

    /// <summary>Die Eintraege, wie die Liste sie zeigt; sie bleiben beim Aendern dieselben Objekte.</summary>
    public ObservableCollection<AppExclusionListItem> ExclusionItems { get; } = [];

    public bool CanDelete => SelectedExclusion is not null;

    /// <summary>Der aufgeklappte Eintrag; er ist zugleich der bearbeitete.</summary>
    public Guid? ExpandedExclusionId
    {
        get => expandedExclusionId;
        private set
        {
            if (SetProperty(ref expandedExclusionId, value))
            {
                foreach (var item in ExclusionItems)
                {
                    item.IsExpanded = item.Id == value;
                }
            }
        }
    }

    public AppExclusion? SelectedExclusion
    {
        get => selectedExclusion;
        set
        {
            if (!SetProperty(ref selectedExclusion, value))
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

    public bool IsEnabled
    {
        get => isEnabled;
        set => SetEditorProperty(ref isEnabled, value);
    }

    /// <summary>
    /// Hinweis darüber, ob der Ausschluss überhaupt ein Merkmal nennt. Ohne Merkmal würde er auf jedes
    /// Fenster passen und die Snap-Funktion vollständig stilllegen; er wird deshalb nicht gespeichert.
    /// </summary>
    public string CriteriaStatus => HasCriteria
        ? string.Empty
        : "Dieser Eintrag greift noch nicht: Trage mindestens eines der drei Merkmale ein – " +
            "Programm, Titel oder Fensterklasse.";

    public bool HasCriteria =>
        processPath.Trim().Length > 0 ||
        windowTitlePattern.Trim().Length > 0 ||
        windowClass.Trim().Length > 0;

    /// <summary>Hinweis im Detail: was ohne Eingrenzung gilt.</summary>
    public string ScopeHint => string.IsNullOrWhiteSpace(processPath)
        ? "Ohne Programm gilt der Eintrag für jedes Fenster, dessen Titel oder Fensterklasse passt."
        : $"Ohne Eingrenzung bleiben alle Fenster von {AppExclusionListItemName()} unangetastet. Trage etwas ein, wenn nur eine bestimmte Fensterart frei bleiben soll.";

    public void AddExclusion() => ResetEditor();

    /// <summary>Traegt ein Programm ein, wie es der Dialog liefert: nur der Dateiname, sofort gespeichert.</summary>
    public AppExclusion AddExclusion(string processName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);
        ResetEditor();
        loading = true;
        try
        {
            processPath = processName.Trim();
            RaiseEditorProperties();
        }
        finally
        {
            loading = false;
        }

        TryPersist();
        return Exclusions.First(exclusion => exclusion.Id == editedExclusionId);
    }

    public void ToggleExpanded(AppExclusionListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (ExpandedExclusionId == item.Id)
        {
            ExpandedExclusionId = null;
            return;
        }

        SelectedExclusion = item.Exclusion;
        ExpandedExclusionId = item.Id;
    }

    public void CollapseAll() => ExpandedExclusionId = null;

    public void SetEnabled(AppExclusionListItem item, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(item);
        var index = Exclusions.ToList().FindIndex(exclusion => exclusion.Id == item.Id);
        if (index < 0 || Exclusions[index].IsEnabled == enabled)
        {
            return;
        }

        var replacement = Exclusions[index] with { IsEnabled = enabled };
        Exclusions[index] = replacement;
        if (selectedExclusion?.Id == replacement.Id)
        {
            selectedExclusion = replacement;
            isEnabled = enabled;
            OnPropertyChanged(nameof(SelectedExclusion));
            OnPropertyChanged(nameof(IsEnabled));
        }

        ExclusionsChanged?.Invoke(Exclusions.ToArray());
    }

    /// <summary>Entfernt einen Eintrag und liefert seine Position fuer «Rueckgaengig».</summary>
    public int RemoveExclusion(AppExclusion exclusion)
    {
        ArgumentNullException.ThrowIfNull(exclusion);
        var index = Exclusions.ToList().FindIndex(candidate => candidate.Id == exclusion.Id);
        if (index < 0)
        {
            return -1;
        }

        if (ExpandedExclusionId == exclusion.Id)
        {
            ExpandedExclusionId = null;
        }

        Exclusions.RemoveAt(index);
        ExclusionsChanged?.Invoke(Exclusions.ToArray());
        if (Exclusions.Count == 0)
        {
            ResetEditor();
        }
        else if (selectedExclusion?.Id == exclusion.Id)
        {
            SelectedExclusion = Exclusions[Math.Min(index, Exclusions.Count - 1)];
        }

        return index;
    }

    public void RestoreExclusion(AppExclusion exclusion, int index)
    {
        ArgumentNullException.ThrowIfNull(exclusion);
        if (Exclusions.Any(candidate => candidate.Id == exclusion.Id))
        {
            return;
        }

        Exclusions.Insert(Math.Clamp(index, 0, Exclusions.Count), exclusion);
        SelectedExclusion = exclusion;
        ExclusionsChanged?.Invoke(Exclusions.ToArray());
    }

    public void DeleteSelectedExclusion()
    {
        if (SelectedExclusion is { } selected)
        {
            RemoveExclusion(selected);
        }
    }

    public void Refresh(IReadOnlyList<AppExclusion> exclusions)
    {
        ArgumentNullException.ThrowIfNull(exclusions);
        var selectedId = SelectedExclusion?.Id ?? editedExclusionId;
        loading = true;
        try
        {
            Exclusions.Clear();
            foreach (var exclusion in exclusions)
            {
                Exclusions.Add(exclusion);
            }
        }
        finally
        {
            loading = false;
        }

        selectedExclusion = null;
        OnPropertyChanged(nameof(SelectedExclusion));
        SelectedExclusion = Exclusions.FirstOrDefault(exclusion => exclusion.Id == selectedId)
            ?? Exclusions.FirstOrDefault();
        if (SelectedExclusion is null)
        {
            ResetEditor();
        }

        if (ExpandedExclusionId is { } expanded && Exclusions.All(exclusion => exclusion.Id != expanded))
        {
            ExpandedExclusionId = null;
        }
    }

    private string AppExclusionListItemName()
    {
        var path = processPath.Trim().Trim('"');
        var name = System.IO.Path.GetFileName(path);
        return name.Length == 0 ? path : name;
    }

    private void SyncItems()
    {
        var byId = ExclusionItems.ToDictionary(item => item.Id);
        for (var index = 0; index < Exclusions.Count; index++)
        {
            var exclusion = Exclusions[index];
            if (byId.TryGetValue(exclusion.Id, out var existing))
            {
                existing.Update(exclusion);
                var currentIndex = ExclusionItems.IndexOf(existing);
                if (currentIndex != index && index < ExclusionItems.Count)
                {
                    ExclusionItems.Move(currentIndex, index);
                }
            }
            else
            {
                ExclusionItems.Insert(
                    Math.Min(index, ExclusionItems.Count),
                    new AppExclusionListItem(exclusion) { IsExpanded = exclusion.Id == expandedExclusionId });
            }
        }

        var ids = Exclusions.Select(exclusion => exclusion.Id).ToHashSet();
        foreach (var stale in ExclusionItems.Where(item => !ids.Contains(item.Id)).ToArray())
        {
            ExclusionItems.Remove(stale);
        }
    }

    private void ResetEditor()
    {
        loading = true;
        try
        {
            selectedExclusion = null;
            OnPropertyChanged(nameof(SelectedExclusion));
            OnPropertyChanged(nameof(CanDelete));
            editedExclusionId = Guid.NewGuid();
            processPath = string.Empty;
            windowTitlePattern = string.Empty;
            windowClass = string.Empty;
            isEnabled = true;
            RaiseEditorProperties();
        }
        finally
        {
            loading = false;
        }
    }

    private void Load(AppExclusion exclusion)
    {
        loading = true;
        try
        {
            editedExclusionId = exclusion.Id;
            processPath = exclusion.ProcessPath;
            windowTitlePattern = exclusion.WindowTitlePattern ?? string.Empty;
            windowClass = exclusion.WindowClass ?? string.Empty;
            isEnabled = exclusion.IsEnabled;
            RaiseEditorProperties();
        }
        finally
        {
            loading = false;
        }
    }

    private void SetEditorProperty<T>(
        ref T field,
        T value,
        [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
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
        if (normalizedProcess.Length > 1024 ||
            WindowTitlePattern.Trim().Length > 512 ||
            WindowClass.Trim().Length > 256 ||
            !HasCriteria)
        {
            return;
        }

        var exclusion = new AppExclusion(
            editedExclusionId,
            normalizedProcess,
            Optional(WindowTitlePattern),
            Optional(WindowClass),
            IsEnabled);
        var index = Exclusions.ToList().FindIndex(candidate => candidate.Id == editedExclusionId);
        if (index >= 0 && Exclusions[index] == exclusion)
        {
            return;
        }

        if (index >= 0)
        {
            Exclusions[index] = exclusion;
        }
        else
        {
            Exclusions.Add(exclusion);
        }

        selectedExclusion = exclusion;
        OnPropertyChanged(nameof(SelectedExclusion));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CriteriaStatus));
        ExclusionsChanged?.Invoke(Exclusions.ToArray());
    }

    private void RaiseEditorProperties()
    {
        OnPropertyChanged(nameof(ProcessPath));
        OnPropertyChanged(nameof(WindowTitlePattern));
        OnPropertyChanged(nameof(WindowClass));
        OnPropertyChanged(nameof(CriteriaStatus));
        OnPropertyChanged(nameof(ScopeHint));
        OnPropertyChanged(nameof(IsEnabled));
    }

    private static string? Optional(string value)
    {
        var normalized = value.Trim();
        return normalized.Length == 0 ? null : normalized;
    }
}
