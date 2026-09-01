using System.Collections.ObjectModel;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using SnapZones.Core.Geometry;
using SnapZones.Core.Monitors;

namespace SnapZones.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private LayoutService layoutService;
    private readonly IReadOnlyList<LiveMonitor> liveMonitors;
    private MonitorChoice? selectedMonitor;
    private MonitorLayout? selectedLayout;
    private LayoutEditorViewModel? editor;
    private string statusMessage = "Bereit";
    private int rememberedWindowCount;
    private string updateStatus = "Noch nicht nach Updates gesucht.";
    private bool isUpdateAvailable;
    private bool isUpdateBusy;
    private string installationStatus = string.Empty;
    private bool canInstall = true;
    private string certificateStatus = string.Empty;
    private string helperStatus = string.Empty;
    private bool isCertificateInstalled;
    private bool suppressPersistence;

    public MainViewModel(SnapConfiguration configuration, IReadOnlyList<LiveMonitor> monitors)
    {
        layoutService = new LayoutService(configuration);
        liveMonitors = monitors;
        Settings = new SettingsViewModel(layoutService.Configuration.Settings);
        Settings.PropertyChanged += Settings_PropertyChanged;
        Monitors = [];
        Layouts = [];
        RefreshMonitors();
        AppRules = new AppRuleEditorViewModel(
            layoutService.Configuration.AppRules,
            RuleTargetLayouts());
        AppRules.RulesChanged += AppRules_RulesChanged;
        AppExclusions = new AppExclusionEditorViewModel(layoutService.Configuration.AppExclusions);
        AppExclusions.ExclusionsChanged += AppExclusions_ExclusionsChanged;
    }

    public event Action<SnapConfiguration>? SaveRequested;

    /// <summary>Bittet darum, saemtliche gemerkten Fensterpositionen zu verwerfen.</summary>
    public event Action? ForgetWindowPositionsRequested;

    /// <summary>Bittet darum, nach einer neueren Veroeffentlichung zu sehen.</summary>
    public event Action? UpdateCheckRequested;

    /// <summary>Bittet darum, die gefundene Veroeffentlichung zu installieren.</summary>
    public event Action? UpdateInstallRequested;

    /// <summary>Bittet darum, das Programm nach «Programme» zu installieren.</summary>
    public event Action? InstallRequested;

    /// <summary>Bittet darum, das eigene Zertifikat einzurichten und den Fensterhelfer zu signieren.</summary>
    public event Action? CertificateInstallRequested;

    /// <summary>Bittet darum, das eigene Zertifikat wieder zu entfernen.</summary>
    public event Action? CertificateRemoveRequested;

    public ObservableCollection<MonitorChoice> Monitors { get; }
    public ObservableCollection<MonitorLayout> Layouts { get; }
    public SettingsViewModel Settings { get; }
    public AppRuleEditorViewModel AppRules { get; }
    public AppExclusionEditorViewModel AppExclusions { get; }

    /// <summary>
    /// Anzahl der gemerkten Fensterpositionen. Wird vom Platzierungs-Modul nachgefuehrt und macht die
    /// sonst unsichtbare Ablage in den Einstellungen sichtbar.
    /// </summary>
    public int RememberedWindowCount
    {
        get => rememberedWindowCount;
        set
        {
            if (SetProperty(ref rememberedWindowCount, value))
            {
                OnPropertyChanged(nameof(RememberedWindowSummary));
                OnPropertyChanged(nameof(HasRememberedWindows));
            }
        }
    }

    public bool HasRememberedWindows => rememberedWindowCount > 0;

    public string RememberedWindowSummary => rememberedWindowCount switch
    {
        0 => "Es ist noch keine Fensterposition gemerkt.",
        1 => "Eine Fensterposition ist gemerkt.",
        _ => $"{rememberedWindowCount} Fensterpositionen sind gemerkt."
    };

    public void ForgetWindowPositions() => ForgetWindowPositionsRequested?.Invoke();

    /// <summary>Die laufende Produktversion im Schema JJJJ.MMTT.NN.</summary>
    public string ProductVersion { get; init; } = string.Empty;

    /// <summary>Was der letzte Blick auf die Veroeffentlichungen ergeben hat, im Klartext.</summary>
    public string UpdateStatus
    {
        get => updateStatus;
        set => SetProperty(ref updateStatus, value);
    }

    /// <summary>Ob eine neuere Veroeffentlichung bereitsteht und angeboten werden darf.</summary>
    public bool IsUpdateAvailable
    {
        get => isUpdateAvailable;
        set
        {
            if (SetProperty(ref isUpdateAvailable, value))
            {
                OnPropertyChanged(nameof(CanInstallUpdate));
            }
        }
    }

    /// <summary>Ob gerade gesucht oder geladen wird. Sperrt beide Schaltflaechen.</summary>
    public bool IsUpdateBusy
    {
        get => isUpdateBusy;
        set
        {
            if (SetProperty(ref isUpdateBusy, value))
            {
                OnPropertyChanged(nameof(CanCheckForUpdates));
                OnPropertyChanged(nameof(CanInstallUpdate));
            }
        }
    }

    public bool CanCheckForUpdates => !isUpdateBusy;
    public bool CanInstallUpdate => isUpdateAvailable && !isUpdateBusy;

    public void CheckForUpdates() => UpdateCheckRequested?.Invoke();

    public void InstallUpdate() => UpdateInstallRequested?.Invoke();

    /// <summary>Wo das Programm liegt und ob es installiert ist, im Klartext.</summary>
    public string InstallationStatus
    {
        get => installationStatus;
        set => SetProperty(ref installationStatus, value);
    }

    /// <summary>Falsch, sobald das Programm bereits aus dem Installationsverzeichnis laeuft.</summary>
    public bool CanInstall
    {
        get => canInstall;
        set => SetProperty(ref canInstall, value);
    }

    public void Install() => InstallRequested?.Invoke();

    /// <summary>Ob das eigene Zertifikat eingerichtet und gültig ist, im Klartext.</summary>
    public string CertificateStatus
    {
        get => certificateStatus;
        set => SetProperty(ref certificateStatus, value);
    }

    /// <summary>Ob der Fensterhelfer läuft, im Klartext.</summary>
    public string HelperStatus
    {
        get => helperStatus;
        set => SetProperty(ref helperStatus, value);
    }

    public bool IsCertificateInstalled
    {
        get => isCertificateInstalled;
        set
        {
            if (SetProperty(ref isCertificateInstalled, value))
            {
                OnPropertyChanged(nameof(CanInstallCertificate));
            }
        }
    }

    public bool CanInstallCertificate => !isCertificateInstalled;

    public void InstallCertificate() => CertificateInstallRequested?.Invoke();

    public void RemoveCertificate() => CertificateRemoveRequested?.Invoke();

    public MonitorChoice? SelectedMonitor
    {
        get => selectedMonitor;
        set
        {
            if (value == selectedMonitor)
            {
                return;
            }

            StoreValidDraft();
            selectedMonitor = value;
            OnPropertyChanged();
            RefreshLayouts();
        }
    }

    public MonitorLayout? SelectedLayout
    {
        get => selectedLayout;
        set
        {
            if (value is null || value.Id == selectedLayout?.Id)
            {
                return;
            }

            StoreValidDraft();
            var activated = layoutService.ActivateLayout(value.Id);
            RefreshMonitors(activated.Monitor);
            StatusMessage = $"Layout «{activated.Name}» auf {GetMonitorDisplayName(activated.Monitor)} aktiv";
            RequestPersistence();
        }
    }

    public LayoutEditorViewModel? Editor => editor;
    public bool CanDeleteSelectedLayout =>
        selectedMonitor is not null && (Layouts.Count > 1 || !selectedMonitor.IsConnected);
    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public SnapConfiguration Configuration => layoutService.Configuration;

    public void Save()
    {
        if (Editor is not null)
        {
            layoutService.UpdateLayout(Editor.CreateSnapshot());
        }

        layoutService.UpdateSettings(Settings.CreateSettings());
        StatusMessage = "Wird gespeichert …";
        SaveRequested?.Invoke(layoutService.Configuration);
    }

    public void AddLayout()
    {
        if (selectedLayout is null)
        {
            return;
        }

        StoreValidDraft();
        var number = 1;
        string name;
        do
        {
            name = $"Layout {number++}";
        }
        while (Layouts.Any(layout => string.Equals(layout.Name, name, StringComparison.CurrentCultureIgnoreCase)));

        var added = layoutService.AddLayout(selectedLayout.Id, name);
        RefreshMonitors(added.Monitor);
        StatusMessage = $"Layout «{added.Name}» erstellt";
        RequestPersistence();
    }

    public void RenameSelectedLayout(string name)
    {
        if (selectedLayout is null)
        {
            return;
        }

        var selectedId = selectedLayout.Id;
        layoutService.RenameLayout(selectedId, name);
        RefreshMonitors(selectedLayout.Monitor, selectedId);
        RequestPersistence();
    }

    public void RenameSelectedMonitor(string? name)
    {
        if (selectedMonitor is null)
        {
            return;
        }

        StoreValidDraft();
        var identity = selectedMonitor.Live.Identity;
        var selectedLayoutId = selectedLayout?.Id;
        layoutService.RenameMonitor(identity, name);
        RefreshMonitors(identity, selectedLayoutId);
        StatusMessage = $"Monitorname geändert: {selectedMonitor?.UserFacingName}";
        RequestPersistence();
    }

    public void MoveSelectedMonitorUp() => MoveSelectedMonitor(-1);

    public void MoveSelectedMonitorDown() => MoveSelectedMonitor(1);

    public string GetMonitorDisplayName(MonitorIdentity monitor)
    {
        var choice = Monitors.FirstOrDefault(candidate =>
            LayoutService.BelongsToMonitor(candidate.Live.Identity, monitor));
        if (choice is not null)
        {
            return choice.UserFacingName;
        }

        var displayNumber = MonitorNaming.ResolveDisplayNumber(monitor, 1);
        return MonitorNaming.UserFacingName(layoutService.CustomMonitorNameFor(monitor), displayNumber);
    }

    public void DeleteSelectedLayout()
    {
        if (selectedLayout is null)
        {
            return;
        }

        var monitor = selectedLayout.Monitor;
        var disconnected = selectedMonitor is { IsConnected: false };
        layoutService.DeleteLayout(selectedLayout.Id, allowRemovingLastLayout: disconnected);
        RefreshMonitors(monitor);
        StatusMessage = disconnected && Monitors.All(choice => !LayoutService.BelongsToMonitor(choice.Live.Identity, monitor))
            ? "Letztes Layout gelöscht – der nicht verbundene Monitor wird nicht mehr aufgeführt"
            : "Layout gelöscht";
        RequestPersistence();
    }

    public void ActivateLayout(Guid layoutId)
    {
        StoreValidDraft();
        var activated = layoutService.ActivateLayout(layoutId);
        var selectedIdentity = selectedMonitor?.Live.Identity;
        RefreshMonitors(selectedIdentity);
        StatusMessage = $"Layout «{activated.Name}» auf {GetMonitorDisplayName(activated.Monitor)} aktiv";
        RequestPersistence();
    }

    public void ReplaceConfiguration(SnapConfiguration replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        suppressPersistence = true;
        try
        {
            layoutService = new LayoutService(replacement);
            Settings.Apply(layoutService.Configuration.Settings);
            AppRules.Refresh(layoutService.Configuration.AppRules, RuleTargetLayouts());
            AppExclusions.Refresh(layoutService.Configuration.AppExclusions);
            RefreshMonitors();
            StatusMessage = "Importierte Konfiguration geladen";
        }
        finally
        {
            suppressPersistence = false;
        }
    }

    private void StoreValidDraft()
    {
        if (Editor is not null && Editor.CanSave)
        {
            layoutService.UpdateLayout(Editor.CreateSnapshot());
        }
    }

    private void RefreshMonitors(MonitorIdentity? preferredMonitor = null, Guid? preferredLayoutId = null)
    {
        var wantedMonitor = preferredMonitor ?? selectedMonitor?.Live.Identity;
        Monitors.Clear();
        var orderedMonitors = liveMonitors
            .Select((live, index) => new
            {
                Live = live,
                OriginalIndex = index,
                OrderIndex = MonitorOrderIndex(MonitorNaming.KeyFor(live.Identity))
            })
            .OrderBy(item => item.OrderIndex)
            .ThenBy(item => item.OriginalIndex)
            .ToArray();
        for (var index = 0; index < orderedMonitors.Length; index++)
        {
            var live = orderedMonitors[index].Live;
            var active = layoutService.EnsureMonitor(
                live.Identity,
                live.WorkArea.Width,
                live.WorkArea.Height);
            var displayNumber = MonitorNaming.ResolveDisplayNumber(live.Identity, index + 1);
            Monitors.Add(new MonitorChoice(
                live,
                active,
                displayNumber,
                layoutService.CustomMonitorNameFor(live.Identity)));
        }

        AddMonitorsWithoutConnection(orderedMonitors.Length);

        selectedMonitor = wantedMonitor is null
            ? Monitors.FirstOrDefault()
            : Monitors.FirstOrDefault(choice => LayoutService.BelongsToMonitor(choice.Live.Identity, wantedMonitor))
              ?? Monitors.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedMonitor));
        RefreshLayouts(preferredLayoutId);
    }

    /// <summary>
    /// Ergänzt Monitore, die nicht angeschlossen sind, für die aber noch Layouts gespeichert sind.
    /// Ohne sie wären diese Layouts in der Oberfläche unerreichbar: sie tauchen weiterhin als Regelziel
    /// auf, lassen sich aber nirgends löschen.
    /// </summary>
    private void AddMonitorsWithoutConnection(int connectedCount)
    {
        var number = connectedCount;
        foreach (var identity in layoutService.MonitorsWithLayouts())
        {
            if (Monitors.Any(choice => LayoutService.BelongsToMonitor(choice.Live.Identity, identity)))
            {
                continue;
            }

            var layouts = layoutService.LayoutsFor(identity);
            if (layouts.Count == 0)
            {
                continue;
            }

            var layout = layouts.FirstOrDefault(candidate => candidate.IsActive) ?? layouts[0];
            number++;
            Monitors.Add(new MonitorChoice(
                new LiveMonitor(
                    identity,
                    new MonitorWorkArea(0, 0, Math.Max(1, layout.SavedWidth), Math.Max(1, layout.SavedHeight)),
                    96,
                    96,
                    false),
                layout,
                MonitorNaming.ResolveDisplayNumber(identity, number),
                layoutService.CustomMonitorNameFor(identity),
                IsConnected: false));
        }
    }

    private void RefreshLayouts(Guid? preferredLayoutId = null)
    {
        Layouts.Clear();
        if (selectedMonitor is null)
        {
            selectedLayout = null;
            ReplaceEditor(null);
        }
        else
        {
            foreach (var layout in layoutService.LayoutsFor(selectedMonitor.Live.Identity))
            {
                Layouts.Add(layout);
            }

            selectedLayout = preferredLayoutId.HasValue
                ? Layouts.FirstOrDefault(layout => layout.Id == preferredLayoutId.Value)
                : null;
            selectedLayout ??= Layouts.FirstOrDefault(layout => layout.IsActive);
            ReplaceEditor(selectedLayout is null ? null : new LayoutEditorViewModel(selectedLayout));
        }

        OnPropertyChanged(nameof(SelectedLayout));
        OnPropertyChanged(nameof(Editor));
        OnPropertyChanged(nameof(CanDeleteSelectedLayout));
    }

    private void ReplaceEditor(LayoutEditorViewModel? replacement)
    {
        if (editor is not null)
        {
            editor.ConfigurationChanged -= Editor_ConfigurationChanged;
        }

        editor = replacement;
        if (editor is not null)
        {
            editor.ConfigurationChanged += Editor_ConfigurationChanged;
        }
    }

    private void Editor_ConfigurationChanged()
    {
        if (editor is null || !editor.IsValid)
        {
            return;
        }

        layoutService.UpdateLayout(editor.CreateSnapshot());
        RequestPersistence();
    }

    private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (suppressPersistence)
        {
            return;
        }

        var settings = Settings.CreateSettings();
        if (!IsValidOverlayColor(settings.OverlayColor))
        {
            StatusMessage = "Ungültige Eingabe";
            return;
        }

        layoutService.UpdateSettings(settings);
        RequestPersistence();
    }

    private void RequestPersistence()
    {
        AppRules.RefreshTargets(RuleTargetLayouts());
        StatusMessage = "Wird gespeichert …";
        SaveRequested?.Invoke(layoutService.Configuration);
    }

    private void AppRules_RulesChanged(IReadOnlyList<SnapZones.Core.AppRules.AppRule> rules)
    {
        if (suppressPersistence)
        {
            return;
        }

        layoutService.UpdateAppRules(rules);
        RequestPersistence();
    }

    private void AppExclusions_ExclusionsChanged(IReadOnlyList<SnapZones.Core.AppRules.AppExclusion> exclusions)
    {
        if (suppressPersistence)
        {
            return;
        }

        layoutService.UpdateAppExclusions(exclusions);
        RequestPersistence();
    }

    private IReadOnlyList<MonitorLayout> RuleTargetLayouts() =>
        layoutService.Configuration.Layouts
            .Select(layout => layout with
            {
                UserFacingMonitorName = GetMonitorDisplayName(layout.Monitor)
            })
            .ToArray();

    private void MoveSelectedMonitor(int offset)
    {
        if (selectedMonitor is null)
        {
            return;
        }

        var currentIndex = Monitors.IndexOf(selectedMonitor);
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= Monitors.Count)
        {
            return;
        }

        var reordered = Monitors.Select(choice => choice.Live.Identity).ToArray();
        (reordered[currentIndex], reordered[targetIndex]) = (reordered[targetIndex], reordered[currentIndex]);
        layoutService.UpdateMonitorOrder(reordered);
        RefreshMonitors(selectedMonitor.Live.Identity, selectedLayout?.Id);
        StatusMessage = "Monitorreihenfolge geändert";
        RequestPersistence();
    }

    private int MonitorOrderIndex(string monitorKey)
    {
        for (var index = 0; index < layoutService.Configuration.MonitorOrder.Count; index++)
        {
            if (string.Equals(layoutService.Configuration.MonitorOrder[index], monitorKey, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private static bool IsValidOverlayColor(string value) =>
        !string.IsNullOrEmpty(value) &&
        value.Length == 7 &&
        value[0] == '#' &&
        value.AsSpan(1).ToString().All(Uri.IsHexDigit);
}
