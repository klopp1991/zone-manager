using System.Collections.ObjectModel;
using SnapZones.Core.Editor;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using SnapZones.Core.Geometry;
using SnapZones.Core.Monitors;
using SnapZones.Core.Persistence;

namespace SnapZones.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private LayoutService layoutService;
    private IReadOnlyList<LiveMonitor> liveMonitors;
    private MonitorChoice? selectedMonitor;
    private MonitorLayout? selectedLayout;
    private LayoutEditorViewModel? editor;
    private string statusMessage = "Bereit";
    private string lastAction = string.Empty;
    private DateTimeOffset? lastSavedAt;
    private int rememberedWindowCount;
    private string updateStatus = "Noch nicht nach Updates gesucht.";
    private bool isUpdateAvailable;
    private bool isUpdateBusy;
    private string installationStatus = string.Empty;
    private bool canInstall = true;
    private string certificateStatus = string.Empty;
    private string helperStatus = string.Empty;
    private bool isCertificateInstalled;
    private bool isCertificateBusy;
    private bool suppressPersistence;
    private SnappingState snappingState = SnappingState.NoActiveLayout;
    private string? pauseReason;
    private string searchQuery = string.Empty;
    private string toastText = string.Empty;
    private Action? toastUndo;
    private bool isToastVisible;

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
        AppRules.RuleItems.CollectionChanged += (_, _) => NotifyCounts();
        AppExclusions = new AppExclusionEditorViewModel(layoutService.Configuration.AppExclusions);
        AppExclusions.ExclusionsChanged += AppExclusions_ExclusionsChanged;
        AppExclusions.Exclusions.CollectionChanged += (_, _) => NotifyCounts();
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

    /// <summary>Bittet darum, das Einrasten nach einem Not-Aus oder Sicherheitsstopp wieder einzuschalten.</summary>
    public event Action? ResumeSnappingRequested;

    /// <summary>Bittet darum, die Liste der frueheren Staende neu zu lesen.</summary>
    public event Action? BackupsRefreshRequested;

    /// <summary>Bittet darum, einen frueheren Stand wiederherzustellen.</summary>
    public event Action<ConfigurationBackup>? RestoreBackupRequested;

    /// <summary>
    /// Zustand der Snap-Funktion, wie ihn Statuszeile und Infobereich zeigen. Wird vom Controller
    /// nachgefuehrt; frueher gab es keinerlei sichtbaren Hinweis darauf, dass ein Not-Aus das Einrasten
    /// bis zum Neustart abgeschaltet hatte.
    /// </summary>
    public SnappingState SnappingState
    {
        get => snappingState;
        set
        {
            if (SetProperty(ref snappingState, value))
            {
                OnPropertyChanged(nameof(SnappingStateLabel));
                OnPropertyChanged(nameof(IsSnappingPaused));
            }
        }
    }

    /// <summary>Warum das Einrasten pausiert ist, im Klartext; leer, solange es laeuft.</summary>
    public string? PauseReason
    {
        get => pauseReason;
        set => SetProperty(ref pauseReason, value);
    }

    public bool IsSnappingPaused => snappingState == SnappingState.Paused;

    public string SnappingStateLabel => snappingState switch
    {
        SnappingState.Active => "Einrasten aktiv",
        SnappingState.Paused => "Einrasten angehalten",
        _ => "Kein aktives Layout"
    };

    public void ResumeSnapping() => ResumeSnappingRequested?.Invoke();

    public ObservableCollection<MonitorChoice> Monitors { get; }
    public ObservableCollection<MonitorLayout> Layouts { get; }
    public SettingsViewModel Settings { get; }
    public AppRuleEditorViewModel AppRules { get; }
    public AppExclusionEditorViewModel AppExclusions { get; }

    /// <summary>Die frueheren Staende der Konfiguration, die juengste zuerst.</summary>
    public ObservableCollection<BackupListItem> Backups { get; } = [];

    /// <summary>Treffer der Einstellungssuche zum aktuellen Suchbegriff.</summary>
    public ObservableCollection<SettingsSearchResult> SearchResults { get; } = [];

    /// <summary>Der Suchbegriff aus der Seitenleiste; jede Aenderung filtert den Index neu.</summary>
    public string SearchQuery
    {
        get => searchQuery;
        set
        {
            if (!SetProperty(ref searchQuery, value ?? string.Empty))
            {
                return;
            }

            SearchResults.Clear();
            foreach (var result in SettingsSearchIndex.Search(searchQuery))
            {
                SearchResults.Add(result);
            }

            OnPropertyChanged(nameof(HasSearchQuery));
            OnPropertyChanged(nameof(HasNoSearchResults));
        }
    }

    public bool HasSearchQuery => searchQuery.Trim().Length > 0;
    public bool HasNoSearchResults => HasSearchQuery && SearchResults.Count == 0;

    public void ClearSearch() => SearchQuery = string.Empty;

    /// <summary>Text des Rueckgaengig-Toasts unten in der Mitte; leer, wenn keiner sichtbar ist.</summary>
    public string ToastText
    {
        get => toastText;
        private set => SetProperty(ref toastText, value);
    }

    public bool IsToastVisible
    {
        get => isToastVisible;
        private set => SetProperty(ref isToastVisible, value);
    }

    public bool CanUndoToast => toastUndo is not null;

    /// <summary>
    /// Zeigt einen Toast mit optionalem Rueckgaengig. Die Loeschung ist zu diesem Zeitpunkt bereits
    /// gespeichert; <paramref name="undo"/> schreibt das Objekt zurueck. Ein neuer Toast ersetzt den alten.
    /// </summary>
    public void ShowToast(string text, Action? undo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        toastUndo = undo;
        ToastText = text;
        OnPropertyChanged(nameof(CanUndoToast));
        IsToastVisible = true;
    }

    public void UndoToast()
    {
        var undo = toastUndo;
        DismissToast();
        undo?.Invoke();
    }

    public void DismissToast()
    {
        toastUndo = null;
        IsToastVisible = false;
        OnPropertyChanged(nameof(CanUndoToast));
    }

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
        set
        {
            if (SetProperty(ref updateStatus, value))
            {
                OnPropertyChanged(nameof(UpdateSummary));
            }
        }
    }

    /// <summary>Untertitel der Zeile «Updates»: Version, letzter Stand der Suche, Sicherheitszusage.</summary>
    public string UpdateSummary =>
        $"Version {ProductVersion} · {TrimEnd(updateStatus)} Nur über HTTPS, geprüft per SHA-256.";

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
        set
        {
            if (SetProperty(ref canInstall, value))
            {
                OnPropertyChanged(nameof(IsInstalled));
            }
        }
    }

    /// <summary>Ob das Programm aus «Programme» laeuft – Voraussetzung fuer den Fensterhelfer.</summary>
    public bool IsInstalled => !canInstall;

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

    /// <summary>
    /// Ob das eigene Zertifikat eingerichtet und gültig ist. Danach richtet sich die einzige
    /// Schaltfläche des Assistenten: einrichten, solange es fehlt — entfernen, sobald es steht.
    /// </summary>
    public bool IsCertificateInstalled
    {
        get => isCertificateInstalled;
        set
        {
            if (SetProperty(ref isCertificateInstalled, value))
            {
                OnPropertyChanged(nameof(CertificateActionLabel));
                OnPropertyChanged(nameof(CertificateActionHint));
                OnPropertyChanged(nameof(CertificateStateLabel));
            }
        }
    }

    /// <summary>
    /// Ob gerade eingerichtet oder entfernt wird. Beides ruft PowerShell auf und dauert Sekunden; die
    /// Schaltflaeche ist solange gesperrt, damit kein zweiter Lauf parallel startet.
    /// </summary>
    public bool IsCertificateBusy
    {
        get => isCertificateBusy;
        set
        {
            if (SetProperty(ref isCertificateBusy, value))
            {
                OnPropertyChanged(nameof(CanToggleCertificate));
            }
        }
    }

    public bool CanToggleCertificate => !isCertificateBusy;

    /// <summary>Der Zustand in zwei Worten, unabhängig von jeder Farbe.</summary>
    public string CertificateStateLabel => isCertificateInstalled
        ? "Eingerichtet"
        : "Nicht eingerichtet";

    public string CertificateActionLabel => isCertificateInstalled
        ? "Zertifikat entfernen"
        : "Zertifikat einrichten";

    public string CertificateActionHint => isCertificateInstalled
        ? "Nimmt das Zertifikat aus allen Speichern. Der Fensterhelfer startet danach nicht mehr, und "
            + "das Programm fragt bei Bedarf wieder nach eigenen Administratorrechten."
        : "Erzeugt ein eigenes Zertifikat, legt es in die Vertrauensspeicher von Windows und signiert "
            + "damit den Fensterhelfer. Windows fragt dabei einmalig nach Administratorrechten.";

    public void InstallCertificate() => CertificateInstallRequested?.Invoke();

    public void RemoveCertificate() => CertificateRemoveRequested?.Invoke();

    /// <summary>
    /// Führt aus, was der Zustand gerade vorsieht. Die Oberfläche zeigt nur eine Schaltfläche; welche
    /// der beiden Richtungen sie auslöst, entscheidet sich hier an einer Stelle.
    /// </summary>
    public void ToggleCertificate()
    {
        if (isCertificateInstalled)
        {
            RemoveCertificate();
        }
        else
        {
            InstallCertificate();
        }
    }

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

    /// <summary>«Monitor 1 von 2» fuer die Kopfzeile der Monitorseite.</summary>
    public string MonitorPositionText => selectedMonitor is null
        ? string.Empty
        : $"Monitor {Monitors.IndexOf(selectedMonitor) + 1} von {Monitors.Count}";

    public bool CanSelectPreviousMonitor => selectedMonitor is not null && Monitors.IndexOf(selectedMonitor) > 0;
    public bool CanSelectNextMonitor => selectedMonitor is not null && Monitors.IndexOf(selectedMonitor) < Monitors.Count - 1;

    public void SelectPreviousMonitor() => SelectMonitorAt(Monitors.IndexOf(selectedMonitor!) - 1);

    public void SelectNextMonitor() => SelectMonitorAt(Monitors.IndexOf(selectedMonitor!) + 1);

    private void SelectMonitorAt(int index)
    {
        if (selectedMonitor is null || index < 0 || index >= Monitors.Count)
        {
            return;
        }

        SelectedMonitor = Monitors[index];
    }

    /// <summary>
    /// Das im Editor bearbeitete Layout. Das Setzen ueber die Auswahl «Aktives Layout» aktiviert es
    /// zugleich auf dem Monitor; <see cref="EditLayout"/> wechselt nur das bearbeitete Layout.
    /// </summary>
    public MonitorLayout? SelectedLayout
    {
        get => selectedLayout;
        set
        {
            if (value is null || value.Id == selectedLayout?.Id && value.IsActive)
            {
                return;
            }

            ActivateLayout(value.Id);
        }
    }

    /// <summary>Wechselt das bearbeitete Layout, ohne es auf dem Monitor zu aktivieren.</summary>
    public void EditLayout(Guid layoutId)
    {
        var layout = Layouts.FirstOrDefault(candidate => candidate.Id == layoutId);
        if (layout is null || layout.Id == selectedLayout?.Id)
        {
            return;
        }

        StoreValidDraft();
        RefreshMonitors(selectedMonitor?.Live.Identity, layoutId);
    }

    public LayoutEditorViewModel? Editor => editor;
    public bool CanDeleteSelectedLayout =>
        selectedMonitor is not null && (Layouts.Count > 1 || !selectedMonitor.IsConnected);
    public string StatusMessage
    {
        get => statusMessage;
        set
        {
            if (SetProperty(ref statusMessage, value) && !IsTransientStatus(value))
            {
                lastAction = value;
            }
        }
    }

    /// <summary>Wann zuletzt gespeichert wurde; null vor dem ersten Speichern in dieser Sitzung.</summary>
    public DateTimeOffset? LastSavedAt
    {
        get => lastSavedAt;
        private set
        {
            if (SetProperty(ref lastSavedAt, value))
            {
                OnPropertyChanged(nameof(LastSavedText));
            }
        }
    }

    /// <summary>«Zuletzt gespeichert vor 2 Minuten» fuer die Uebersicht.</summary>
    public string LastSavedText => DescribeLastSaved(lastSavedAt, DateTimeOffset.Now);

    public static string DescribeLastSaved(DateTimeOffset? savedAt, DateTimeOffset now)
    {
        if (savedAt is not { } moment)
        {
            return "In dieser Sitzung noch nichts geändert";
        }

        var elapsed = now - moment;
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "Zuletzt gespeichert gerade eben";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            var minutes = (int)elapsed.TotalMinutes;
            return minutes == 1 ? "Zuletzt gespeichert vor einer Minute" : $"Zuletzt gespeichert vor {minutes} Minuten";
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            var hours = (int)elapsed.TotalHours;
            return hours == 1 ? "Zuletzt gespeichert vor einer Stunde" : $"Zuletzt gespeichert vor {hours} Stunden";
        }

        return $"Zuletzt gespeichert am {moment:dd.MM. HH:mm}";
    }

    /// <summary>Der Controller meldet ein erfolgreiches Speichern; die Statuszeile nennt Aktion und Uhrzeit.</summary>
    public void MarkSaved()
    {
        LastSavedAt = DateTimeOffset.Now;
        var action = lastAction.Length > 0 ? $" · {lastAction}" : string.Empty;
        statusMessage = $"✓ Gespeichert{action} ({LastSavedAt:HH:mm})";
        OnPropertyChanged(nameof(StatusMessage));
    }

    /// <summary>Laesst die relative Zeitangabe der Uebersicht nachrechnen; die Oberflaeche ruft das im Takt auf.</summary>
    public void RefreshLastSavedText() => OnPropertyChanged(nameof(LastSavedText));

    public SnapConfiguration Configuration => layoutService.Configuration;

    public int MonitorCount => Monitors.Count;
    public int LayoutCount => layoutService.Configuration.Layouts.Count;
    public int RuleCount => AppRules.RuleItems.Count;
    public int ExclusionCount => AppExclusions.Exclusions.Count;
    public int PausedRuleCount => AppRules.PausedCount;
    public bool HasPausedRules => PausedRuleCount > 0;

    /// <summary>Untertitel der Zaehlerkarte «Zugeordnete Fenster».</summary>
    public string RuleCountHint => RuleCount == 0
        ? "Noch keins – ein Fenster auf eine Zone ziehen"
        : PausedRuleCount switch
        {
            0 => "Alle laufen",
            1 => "1 pausiert – Ziel fehlt",
            _ => $"{PausedRuleCount} pausiert – Ziel fehlt"
        };

    public string MonitorCountText => Monitors.Count.ToString();
    public string LayoutCountText => LayoutCount.ToString();
    public string RuleCountText => RuleCount.ToString();
    public string ExclusionCountText => ExclusionCount.ToString();

    public void Save()
    {
        if (Editor is not null)
        {
            layoutService.UpdateLayout(Editor.CreateSnapshot());
        }

        layoutService.UpdateSettings(Settings.CreateSettings());
        layoutService.RecordMonitorSet(liveMonitors);
        StatusMessage = "Wird gespeichert …";
        SaveRequested?.Invoke(layoutService.Configuration);
    }

    /// <summary>
    /// Uebernimmt die neu erkannten Monitore und, falls die Abstimmung sie veraendert hat, die
    /// Konfiguration. Ein gueltiger Entwurf im Editor wird vorher gesichert; gespeichert wird nicht
    /// von hier aus, das entscheidet der Aufrufer.
    /// </summary>
    public void ReplaceMonitors(IReadOnlyList<LiveMonitor> monitors, SnapConfiguration? configuration)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        suppressPersistence = true;
        try
        {
            StoreValidDraft();
            liveMonitors = monitors;
            if (configuration is not null)
            {
                layoutService = new LayoutService(configuration);
            }

            RefreshMonitors(selectedMonitor?.Live.Identity, selectedLayout?.Id);
            AppRules.RefreshTargets(RuleTargetLayouts());
        }
        finally
        {
            suppressPersistence = false;
        }
    }

    public void AddLayout()
    {
        if (selectedLayout is null)
        {
            return;
        }

        StoreValidDraft();
        var added = layoutService.AddLayout(selectedLayout.Id, NextLayoutName("Layout"));
        RefreshMonitors(added.Monitor);
        StatusMessage = $"Layout «{added.Name}» erstellt";
        RequestPersistence();
    }

    /// <summary>Ein neues Layout mit einer einzigen Zone ueber die ganze Flaeche.</summary>
    public void AddEmptyLayout()
    {
        AddLayout();
        Editor?.ReplaceZones([new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)]);
    }

    /// <summary>Ein neues Layout aus einer der Vorlagen.</summary>
    public void AddLayoutFromTemplate(LayoutTemplate template)
    {
        AddLayout();
        Editor?.ApplyTemplate(template);
    }

    /// <summary>Eine Kopie des bearbeiteten Layouts unter neuem Namen.</summary>
    public void DuplicateSelectedLayout()
    {
        if (selectedLayout is null)
        {
            return;
        }

        StoreValidDraft();
        var added = layoutService.AddLayout(selectedLayout.Id, NextLayoutName($"{selectedLayout.Name} Kopie"));
        RefreshMonitors(added.Monitor);
        StatusMessage = $"Layout «{added.Name}» als Kopie erstellt";
        RequestPersistence();
    }

    /// <summary>«Layout 1», «Layout 2» … beziehungsweise «Arbeiten Kopie», «Arbeiten Kopie 2» ….</summary>
    private string NextLayoutName(string stem)
    {
        var numbered = string.Equals(stem, "Layout", StringComparison.Ordinal);
        if (!numbered && Layouts.All(layout => !string.Equals(layout.Name, stem, StringComparison.CurrentCultureIgnoreCase)))
        {
            return stem;
        }

        var number = numbered ? 1 : 2;
        string name;
        do
        {
            name = $"{stem} {number++}";
        }
        while (Layouts.Any(layout => string.Equals(layout.Name, name, StringComparison.CurrentCultureIgnoreCase)));
        return name;
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
        StatusMessage = $"Layout in «{name.Trim()}» umbenannt";
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

    /// <summary>
    /// Loescht das bearbeitete Layout und liefert es zurueck, damit der Toast es ueber
    /// <see cref="RestoreLayout"/> wiederherstellen kann. Null, wenn nichts geloescht wurde.
    /// </summary>
    public MonitorLayout? DeleteSelectedLayout()
    {
        if (selectedLayout is null)
        {
            return null;
        }

        var deleted = selectedLayout;
        var monitor = selectedLayout.Monitor;
        var disconnected = selectedMonitor is { IsConnected: false };
        layoutService.DeleteLayout(selectedLayout.Id, allowRemovingLastLayout: disconnected);
        RefreshMonitors(monitor);
        StatusMessage = disconnected && Monitors.All(choice => !LayoutService.BelongsToMonitor(choice.Live.Identity, monitor))
            ? "Letztes Layout gelöscht – der nicht verbundene Monitor wird nicht mehr aufgeführt"
            : $"Layout «{deleted.Name}» gelöscht";
        RequestPersistence();
        return deleted;
    }

    /// <summary>Schreibt ein geloeschtes Layout zurueck – der Rueckweg aus dem Toast.</summary>
    public void RestoreLayout(MonitorLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        StoreValidDraft();
        var restored = layoutService.RestoreLayout(layout);
        RefreshMonitors(restored.Monitor, restored.Id);
        StatusMessage = $"Layout «{restored.Name}» wiederhergestellt";
        RequestPersistence();
    }

    public void ActivateLayout(Guid layoutId)
    {
        StoreValidDraft();
        var activated = layoutService.ActivateLayout(layoutId);
        var selectedIdentity = selectedMonitor?.Live.Identity;
        // Das aktivierte Layout wird zugleich bearbeitet, sofern es auf dem gewaehlten Monitor liegt.
        var preferredLayout = selectedIdentity is not null && LayoutService.BelongsToMonitor(activated.Monitor, selectedIdentity)
            ? activated.Id
            : selectedLayout?.Id;
        RefreshMonitors(selectedIdentity, preferredLayout);
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

    /// <summary>Ersetzt die Liste der frueheren Staende durch das, was der Controller gelesen hat.</summary>
    public void ReplaceBackups(IReadOnlyList<ConfigurationBackup> backups)
    {
        ArgumentNullException.ThrowIfNull(backups);
        Backups.Clear();
        foreach (var backup in backups)
        {
            Backups.Add(new BackupListItem(backup));
        }

        OnPropertyChanged(nameof(HasBackups));
    }

    public bool HasBackups => Backups.Count > 0;

    public void RefreshBackups() => BackupsRefreshRequested?.Invoke();

    public void RestoreBackup(BackupListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        RestoreBackupRequested?.Invoke(item.Backup);
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
                layoutService.CustomMonitorNameFor(live.Identity))
            {
                Layouts = layoutService.LayoutsFor(live.Identity)
            });
        }

        AddMonitorsWithoutConnection(orderedMonitors.Length);

        selectedMonitor = wantedMonitor is null
            ? Monitors.FirstOrDefault()
            : Monitors.FirstOrDefault(choice => LayoutService.BelongsToMonitor(choice.Live.Identity, wantedMonitor))
              ?? Monitors.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedMonitor));
        OnPropertyChanged(nameof(MonitorPositionText));
        OnPropertyChanged(nameof(CanSelectPreviousMonitor));
        OnPropertyChanged(nameof(CanSelectNextMonitor));
        RefreshLayouts(preferredLayoutId);
        NotifyCounts();
    }

    /// <summary>
    /// Ergänzt Monitore, die nicht angeschlossen sind, für die aber noch Layouts gespeichert sind.
    /// Ohne sie wären diese Layouts in der Oberfläche unerreichbar: sie tauchen weiterhin als Ziel
    /// einer Zuordnung auf, lassen sich aber nirgends löschen.
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
                IsConnected: false)
            {
                Layouts = layouts
            });
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
        StatusMessage = $"Layout «{selectedLayout?.Name}» geändert";
        RequestPersistence();
    }

    private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;
        if (suppressPersistence || eventArgs.PropertyName == nameof(SettingsViewModel.BehaviourTabIndex))
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
        if (eventArgs.PropertyName is { } name && name != nameof(SettingsViewModel.EditorValuePanelOpen))
        {
            StatusMessage = "Einstellung geändert";
        }

        RequestPersistence();
    }

    private void RequestPersistence()
    {
        AppRules.RefreshTargets(RuleTargetLayouts());
        layoutService.RecordMonitorSet(liveMonitors);
        NotifyCounts();
        statusMessage = "Wird gespeichert …";
        OnPropertyChanged(nameof(StatusMessage));
        SaveRequested?.Invoke(layoutService.Configuration);
    }

    private void AppRules_RulesChanged(IReadOnlyList<SnapZones.Core.AppRules.AppRule> rules)
    {
        if (suppressPersistence)
        {
            return;
        }

        layoutService.UpdateAppRules(rules);
        StatusMessage = "Zuordnungen geändert";
        RequestPersistence();
    }

    private void AppExclusions_ExclusionsChanged(IReadOnlyList<SnapZones.Core.AppRules.AppExclusion> exclusions)
    {
        if (suppressPersistence)
        {
            return;
        }

        layoutService.UpdateAppExclusions(exclusions);
        StatusMessage = "Liste «In Ruhe lassen» geändert";
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

    private void NotifyCounts()
    {
        OnPropertyChanged(nameof(MonitorCount));
        OnPropertyChanged(nameof(LayoutCount));
        OnPropertyChanged(nameof(RuleCount));
        OnPropertyChanged(nameof(ExclusionCount));
        OnPropertyChanged(nameof(PausedRuleCount));
        OnPropertyChanged(nameof(HasPausedRules));
        OnPropertyChanged(nameof(RuleCountHint));
        OnPropertyChanged(nameof(MonitorCountText));
        OnPropertyChanged(nameof(LayoutCountText));
        OnPropertyChanged(nameof(RuleCountText));
        OnPropertyChanged(nameof(ExclusionCountText));
    }

    private static bool IsTransientStatus(string value) =>
        value.StartsWith("Wird gespeichert", StringComparison.Ordinal) ||
        value.StartsWith("✓ Gespeichert", StringComparison.Ordinal) ||
        value.Length == 0;

    private static string TrimEnd(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 || trimmed.EndsWith('.') ? trimmed : $"{trimmed}.";
    }

    private static bool IsValidOverlayColor(string value) =>
        !string.IsNullOrEmpty(value) &&
        value.Length == 7 &&
        value[0] == '#' &&
        value.AsSpan(1).ToString().All(Uri.IsHexDigit);
}
