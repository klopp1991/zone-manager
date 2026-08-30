using System.Collections.ObjectModel;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
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
    }

    public event Action<SnapConfiguration>? SaveRequested;

    public ObservableCollection<MonitorChoice> Monitors { get; }
    public ObservableCollection<MonitorLayout> Layouts { get; }
    public SettingsViewModel Settings { get; }

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
    public bool CanDeleteSelectedLayout => selectedMonitor is not null && Layouts.Count > 1;
    public string LayoutSummary => $"{liveMonitors.Count} Monitore · {layoutService.Configuration.Layouts.Count} Layouts";

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
        layoutService.DeleteLayout(selectedLayout.Id);
        RefreshMonitors(monitor);
        StatusMessage = "Layout gelöscht";
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
        for (var index = 0; index < liveMonitors.Count; index++)
        {
            var live = liveMonitors[index];
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

        selectedMonitor = wantedMonitor is null
            ? Monitors.FirstOrDefault()
            : Monitors.FirstOrDefault(choice => LayoutService.BelongsToMonitor(choice.Live.Identity, wantedMonitor))
              ?? Monitors.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedMonitor));
        RefreshLayouts(preferredLayoutId);
        OnPropertyChanged(nameof(LayoutSummary));
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
            selectedLayout ??= Layouts.SingleOrDefault(layout => layout.IsActive);
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
        StatusMessage = "Wird gespeichert …";
        SaveRequested?.Invoke(layoutService.Configuration);
    }

    private static bool IsValidOverlayColor(string value) =>
        !string.IsNullOrEmpty(value) &&
        value.Length == 7 &&
        value[0] == '#' &&
        value.AsSpan(1).ToString().All(Uri.IsHexDigit);
}
