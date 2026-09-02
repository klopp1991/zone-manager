using System.Collections.ObjectModel;
using SnapZones.Core.AppRules;

namespace SnapZones.App.ViewModels;

/// <summary>
/// Verwaltet die Liste der ausgeschlossenen Fenster. Ein Ausschluss ist bewusst schlanker als eine
/// App-Regel: er hat kein Ziel, kein Ereignis und keine Priorität, weil er nichts anordnet, sondern
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
    private bool loading;

    public AppExclusionEditorViewModel(IReadOnlyList<AppExclusion> exclusions)
    {
        ArgumentNullException.ThrowIfNull(exclusions);
        Exclusions = new ObservableCollection<AppExclusion>(exclusions);
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

    public bool CanDelete => SelectedExclusion is not null;

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
        : "Dieser Ausschluss greift noch nicht: Trage mindestens eines der drei Merkmale ein – " +
            "Programm, Titelmuster oder Fensterklasse.";

    public bool HasCriteria =>
        processPath.Trim().Length > 0 ||
        windowTitlePattern.Trim().Length > 0 ||
        windowClass.Trim().Length > 0;

    public void AddExclusion() => ResetEditor();

    public void DeleteSelectedExclusion()
    {
        if (SelectedExclusion is not { } selected)
        {
            return;
        }

        var index = Exclusions.IndexOf(selected);
        Exclusions.Remove(selected);
        ExclusionsChanged?.Invoke(Exclusions.ToArray());
        if (Exclusions.Count == 0)
        {
            ResetEditor();
            return;
        }

        SelectedExclusion = Exclusions[Math.Min(index, Exclusions.Count - 1)];
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
        OnPropertyChanged(nameof(IsEnabled));
    }

    private static string? Optional(string value)
    {
        var normalized = value.Trim();
        return normalized.Length == 0 ? null : normalized;
    }
}
