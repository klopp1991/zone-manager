using System.Globalization;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using SnapZones.App.Controls;
using SnapZones.App.ViewModels;
using SnapZones.Core.AppRules;
using SnapZones.Core.Editor;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;

namespace SnapZones.App.Views;

public partial class MainWindow : Window
{
    private MainViewModel? viewModel;
    private readonly Func<string, string?> pickOverlayColor;
    private readonly DispatcherTimer toastTimer;
    private readonly DispatcherTimer savedTextTimer;
    private LayoutEditorViewModel? observedEditor;
    private FullscreenZoneEditorWindow? fullscreenEditor;
    private Guid? renamingZoneId;
    private MonitorLayout? renamingLayout;

    public event Func<string, Task>? ExportConfigurationRequested;
    public event Func<string, Task>? ImportConfigurationRequested;
    public event Action? IdentifyMonitorsRequested;

    /// <summary>Bittet darum, die aktiven Layouts aller Monitore drei Sekunden lang als Overlay zu zeigen.</summary>
    public event Action? PreviewActiveLayoutsRequested;

    /// <summary>
    /// Wird ausgeloest, sobald die Seite «Programm» sichtbar wird. Zertifikat, Fensterhelfer und die
    /// frueheren Staende koennen sich ausserhalb des Programms geaendert haben; ihr Zustand wird deshalb
    /// beim Oeffnen neu gelesen statt einmal beim Start.
    /// </summary>
    public event Action? SettingsPageOpened;

    public MainWindow()
        : this(null)
    {
    }

    public MainWindow(Func<string, string?>? pickOverlayColor)
    {
        this.pickOverlayColor = pickOverlayColor ?? PickOverlayColorWithDialog;
        InitializeComponent();
        VersionLabel.Text = ProductInfo.Version;
        AboutVersionText.Text = $"{ProductInfo.Version} · .NET 8 · Windows 10 ab 1809";
        NavigationTabs.SelectedItem = LayoutsTab;
        NavigationTabs.SelectionChanged += NavigationTabs_SelectionChanged;
        toastTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromSeconds(6) };
        toastTimer.Tick += (_, _) =>
        {
            toastTimer.Stop();
            viewModel?.DismissToast();
        };
        savedTextTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromSeconds(30) };
        savedTextTimer.Tick += (_, _) => viewModel?.RefreshLastSavedText();
        Closed += (_, _) =>
        {
            toastTimer.Stop();
            savedTextTimer.Stop();
            fullscreenEditor?.Close();
        };
    }

    private void NavigationTabs_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        _ = sender;
        if (!ReferenceEquals(eventArgs.OriginalSource, NavigationTabs))
        {
            return;
        }

        if (eventArgs.AddedItems.Count == 0)
        {
            return;
        }

        var page = eventArgs.AddedItems[0];
        if (ReferenceEquals(page, StartupTab) ||
            ReferenceEquals(page, SystemTab) ||
            ReferenceEquals(page, FullscreenTab) ||
            ReferenceEquals(page, AboutTab) ||
            ReferenceEquals(page, BackupTab))
        {
            SettingsPageOpened?.Invoke();
            viewModel?.RefreshBackups();
        }
    }

    public void AttachViewModel(MainViewModel model)
    {
        if (viewModel is not null)
        {
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            viewModel.Settings.PropertyChanged -= Settings_PropertyChanged;
        }

        ObserveEditor(null);
        viewModel = model;
        DataContext = model;
        model.PropertyChanged += ViewModel_PropertyChanged;
        model.Settings.PropertyChanged += Settings_PropertyChanged;
        ObserveEditor(model.Editor);
        ZoneValues.IsFullscreenDriverMissing = !model.IsDisplayDriverInstalled;
        RefreshEditor();
        savedTextTimer.Start();
    }

    /// <summary>Wechselt zu einer Seite; wird von Suche, Kopfzeile und Infobereich benutzt.</summary>
    public void ShowPage(NavigationPage page, string? tuningSection = null, string? highlightLabel = null)
    {
        var tab = page switch
        {
            NavigationPage.Layouts => LayoutsTab,
            NavigationPage.Monitors => MonitorsTab,
            NavigationPage.Rules => RulesTab,
            NavigationPage.Exclusions => ExclusionsTab,
            NavigationPage.Drag => DragTab,
            NavigationPage.Appearance => AppearanceTab,
            NavigationPage.Spacing => SpacingTab,
            NavigationPage.Memory => MemoryTab,
            NavigationPage.Fullscreen => FullscreenTab,
            NavigationPage.Keys => KeysTab,
            NavigationPage.Startup => StartupTab,
            NavigationPage.Backup => BackupTab,
            NavigationPage.System => SystemTab,
            _ => AboutTab
        };
        NavigationTabs.SelectedItem = tab;
        if (tuningSection is not null)
        {
            ExpandTuning(tab);
        }

        if (highlightLabel is { Length: > 0 })
        {
            // Erst nach dem Aufbau der Seite: vorher hat die Zeile weder Platz noch Position.
            _ = Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() => HighlightSetting(tab, highlightLabel)));
        }
    }

    /// <summary>
    /// Holt die Zeile eines Suchtreffers in Sicht und hebt sie kurz hervor. Gescrollt wird die Zeile
    /// selbst, nicht die ganze Seite; sonst springt der Blick an eine beliebige Stelle.
    /// </summary>
    private void HighlightSetting(TabItem tab, string label)
    {
        var text = UiTuningLabels(tab).FirstOrDefault(candidate =>
            string.Equals(candidate.Text, label, StringComparison.Ordinal));
        if (text is null || FindSettingRow(text) is not { } row)
        {
            return;
        }

        row.BringIntoView();
        var previous = row.Background;
        row.SetResourceReference(Border.BackgroundProperty, "HoverBrush");
        var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(1200)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            row.Background = previous;
        };
        timer.Start();
    }

    private static IEnumerable<TextBlock> UiTuningLabels(DependencyObject root)
    {
        foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is TextBlock text)
            {
                yield return text;
            }

            foreach (var found in UiTuningLabels(child))
            {
                yield return found;
            }
        }
    }

    /// <summary>Die Einstellungszeile, in der ein Element steht; <c>null</c>, wenn es in keiner steht.</summary>
    private static Border? FindSettingRow(DependencyObject element)
    {
        var settingRow = System.Windows.Application.Current?.Resources["SettingRow"] as Style;
        var settingRowLast = System.Windows.Application.Current?.Resources["SettingRowLast"] as Style;
        for (var current = element; current is not null; current = System.Windows.Media.VisualTreeHelper.GetParent(current))
        {
            if (current is Border border &&
                (ReferenceEquals(border.Style, settingRow) || ReferenceEquals(border.Style, settingRowLast)))
            {
                return border;
            }
        }

        return null;
    }

    /// <summary>
    /// Klappt die Feinabstimmung einer Seite auf. Ein Suchtreffer, der dorthin fuehrt, waere sonst
    /// unsichtbar; sonst bleibt der Abschnitt zu und merkt sich nichts ueber die Sitzung hinaus.
    /// </summary>
    private static void ExpandTuning(TabItem tab)
    {
        foreach (var expander in UiTuningSections(tab))
        {
            expander.IsExpanded = true;
        }
    }

    private static IEnumerable<TuningExpander> UiTuningSections(DependencyObject root)
    {
        if (root is TuningExpander expander)
        {
            yield return expander;
            yield break;
        }

        foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            foreach (var found in UiTuningSections(child))
            {
                yield return found;
            }
        }
    }

    /// <summary>Wechselt auf «Aussehen &amp; Start»; wird vom Infobereich aufgerufen.</summary>
    public void ShowSettingsPage() => ShowPage(NavigationPage.Startup);

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;
        if (eventArgs.PropertyName == nameof(MainViewModel.IsDisplayDriverInstalled))
        {
            ZoneValues.IsFullscreenDriverMissing = viewModel?.IsDisplayDriverInstalled == false;
        }

        if (eventArgs.PropertyName == nameof(MainViewModel.Editor))
        {
            ObserveEditor(viewModel?.Editor);
        }

        if (eventArgs.PropertyName is nameof(MainViewModel.Editor) or
            nameof(MainViewModel.SelectedMonitor) or
            nameof(MainViewModel.SelectedLayout))
        {
            RefreshEditor();
        }

        if (eventArgs.PropertyName == nameof(MainViewModel.IsToastVisible))
        {
            toastTimer.Stop();
            if (viewModel?.IsToastVisible == true)
            {
                toastTimer.Start();
            }
        }
    }

    private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;
        if (eventArgs.PropertyName is nameof(SettingsViewModel.HighlightColor) or nameof(SettingsViewModel.OverlayColor))
        {
            RefreshHighlightPreview();
        }

        if (eventArgs.PropertyName == nameof(SettingsViewModel.MagnetThresholdPixels))
        {
            RefreshEditor();
        }
    }

    private void ObserveEditor(LayoutEditorViewModel? editor)
    {
        if (ReferenceEquals(observedEditor, editor))
        {
            return;
        }

        if (observedEditor is not null)
        {
            observedEditor.PropertyChanged -= Editor_PropertyChanged;
        }

        observedEditor = editor;
        if (observedEditor is not null)
        {
            observedEditor.PropertyChanged += Editor_PropertyChanged;
        }
    }

    private void Editor_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;
        if (!ZoneValues.IsApplyingChange && eventArgs.PropertyName == nameof(LayoutEditorViewModel.Zones))
        {
            RefreshEditor();
        }
    }

    // ----------------------------------------------------------------- Navigation, Suche, Toast

    private void ShowStartPage_Click(object sender, RoutedEventArgs eventArgs) => ShowPage(NavigationPage.Layouts);

    private void ShowRules_Click(object sender, RoutedEventArgs eventArgs) => ShowPage(NavigationPage.Rules);

    private void ShowExclusions_Click(object sender, RoutedEventArgs eventArgs) => ShowPage(NavigationPage.Exclusions);

    private void ShowRemembered_Click(object sender, RoutedEventArgs eventArgs) => ShowPage(NavigationPage.Memory);

    private void SearchResult_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = eventArgs;
        if (sender is System.Windows.Controls.Button { DataContext: SettingsSearchResult result })
        {
            NavigateTo(result);
        }
    }

    private void NavigateTo(SettingsSearchResult result)
    {
        ShowPage(result.Page, result.TuningSection, result.Label);
        viewModel?.ClearSearch();
    }

    private void SearchText_KeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        _ = sender;
        if (eventArgs.Key == Key.Escape)
        {
            viewModel?.ClearSearch();
            eventArgs.Handled = true;
        }
        else if (eventArgs.Key == Key.Enter && viewModel?.SearchResults.FirstOrDefault() is { } first)
        {
            NavigateTo(first);
            eventArgs.Handled = true;
        }
    }

    private void Toast_MouseEnter(object sender, System.Windows.Input.MouseEventArgs eventArgs) => toastTimer.Stop();

    private void Toast_MouseLeave(object sender, System.Windows.Input.MouseEventArgs eventArgs)
    {
        if (viewModel?.IsToastVisible == true)
        {
            toastTimer.Start();
        }
    }

    private void ToastUndo_Click(object sender, RoutedEventArgs eventArgs)
    {
        toastTimer.Stop();
        viewModel?.UndoToast();
        RefreshEditor();
    }

    private void ToastClose_Click(object sender, RoutedEventArgs eventArgs)
    {
        toastTimer.Stop();
        viewModel?.DismissToast();
    }

    private void PreviewZones_Click(object sender, RoutedEventArgs eventArgs) => PreviewActiveLayoutsRequested?.Invoke();

    // ----------------------------------------------------------------- Monitore

    private void MonitorTab_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = eventArgs;
        if (viewModel is not null && sender is FrameworkElement { DataContext: MonitorChoice choice })
        {
            viewModel.SelectedMonitor = choice;
            RefreshEditor();
        }
    }

    private void MoveMonitorLeft_Click(object sender, RoutedEventArgs eventArgs)
    {
        eventArgs.Handled = true;
        viewModel?.MoveMonitorLeft();
    }

    private void MoveMonitorRight_Click(object sender, RoutedEventArgs eventArgs)
    {
        eventArgs.Handled = true;
        viewModel?.MoveMonitorRight();
    }

    private void MoveLayoutLeft_Click(object sender, RoutedEventArgs eventArgs)
    {
        eventArgs.Handled = true;
        viewModel?.MoveLayoutLeft();
    }

    private void MoveLayoutRight_Click(object sender, RoutedEventArgs eventArgs)
    {
        eventArgs.Handled = true;
        viewModel?.MoveLayoutRight();
    }

    private void EditActiveLayout_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel?.SelectedMonitor is { } monitor)
        {
            viewModel.EditLayout(monitor.Layout.Id);
        }

        ShowPage(NavigationPage.Layouts);
    }

    private void MonitorName_LostFocus(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        CommitMonitorName();
    }

    private void MonitorName_KeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Enter)
        {
            CommitMonitorName();
            eventArgs.Handled = true;
        }
    }

    private void CommitMonitorName()
    {
        if (viewModel?.SelectedMonitor is null)
        {
            return;
        }

        var enteredName = MonitorNameText.Text;
        var normalisedName = string.IsNullOrWhiteSpace(enteredName) ? null : enteredName.Trim();
        if (string.Equals(normalisedName, viewModel.SelectedMonitor.CustomName, StringComparison.Ordinal))
        {
            MonitorNameText.Text = viewModel.SelectedMonitor.CustomName ?? string.Empty;
            return;
        }

        try
        {
            viewModel.RenameSelectedMonitor(enteredName);
        }
        catch (Exception exception)
        {
            MonitorNameText.Text = viewModel.SelectedMonitor.CustomName ?? string.Empty;
            viewModel.StatusMessage = exception.Message;
        }
    }

    private void IdentifyMonitors_Click(object sender, RoutedEventArgs eventArgs) => IdentifyMonitorsRequested?.Invoke();

    private void OpenSystemSetting_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string requested })
        {
            return;
        }

        var uri = requested switch
        {
            "ms-settings:display" => requested,
            "ms-settings:easeofaccess-textsize" => requested,
            "ms-settings:taskbar" => requested,
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(uri))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            if (viewModel is not null)
            {
                viewModel.StatusMessage = $"Windows-Einstellung konnte nicht geöffnet werden: {exception.Message}";
            }
        }
    }

    private void RefreshScalingPage()
    {
        var choice = viewModel?.SelectedMonitor;
        var monitor = choice?.Live;
        if (monitor is null)
        {
            ScalingFactorText.Text = "–";
            ScalingResolutionText.Text = "–";
            ScalingWorkAreaText.Text = "–";
            ScalingPhysicalSizeText.Text = "–";
            WindowsScaleText.Text = "Kein Monitor ausgewählt.";
            return;
        }

        var scalePercent = Math.Round(monitor.DpiX / 96d * 100);
        ScalingFactorText.Text = $"{scalePercent:0} %";
        ScalingResolutionText.Text = $"{monitor.MonitorBounds.Width} × {monitor.MonitorBounds.Height}";
        ScalingWorkAreaText.Text = $"{monitor.WorkArea.Width} × {monitor.WorkArea.Height}";
        ScalingPhysicalSizeText.Text = PhysicalSizeText(monitor);
        var hardware = string.IsNullOrWhiteSpace(monitor.Identity.HardwareId)
            ? "keine EDID-Kennung von Windows erhalten"
            : "Kennung aus EDID erkannt";
        WindowsScaleText.Text = $"{monitor.Identity.FriendlyName} · {hardware} · {monitor.DpiX:0} DPI";
    }

    private static string PhysicalSizeText(LiveMonitor monitor)
    {
        if (monitor.PhysicalWidthCentimeters is not { } width ||
            monitor.PhysicalHeightCentimeters is not { } height ||
            width <= 0 ||
            height <= 0)
        {
            return "unbekannt";
        }

        var diagonalInches = Math.Sqrt(width * width + height * height) / 2.54d;
        return $"{diagonalInches:0.#}″";
    }

    // ----------------------------------------------------------------- Zonen & Layouts

    private void Monitor_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs) => RefreshEditor();

    private void LayoutTab_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = eventArgs;
        if (viewModel is not null && sender is FrameworkElement { DataContext: MonitorLayout layout })
        {
            viewModel.EditLayout(layout.Id);
            RefreshEditor();
        }
    }

    private void LayoutTab_RightClick(object sender, MouseButtonEventArgs eventArgs)
    {
        if (viewModel is null || sender is not FrameworkElement { DataContext: MonitorLayout layout } element)
        {
            return;
        }

        viewModel.EditLayout(layout.Id);
        RefreshEditor();
        var menu = new ContextMenu { PlacementTarget = element, Placement = PlacementMode.Bottom };
        var activate = new MenuItem { Header = "Aktivieren", IsEnabled = !layout.IsActive };
        activate.Click += (_, _) => viewModel.ActivateLayout(layout.Id);
        var rename = new MenuItem { Header = "Umbenennen …" };
        rename.Click += (_, _) => BeginLayoutRename(element, layout);
        var duplicate = new MenuItem { Header = "Duplizieren" };
        duplicate.Click += (_, _) => viewModel.DuplicateSelectedLayout();
        var delete = new MenuItem { Header = "Layout löschen", IsEnabled = viewModel.CanDeleteSelectedLayout };
        delete.SetResourceReference(ForegroundProperty, "DangerBrush");
        delete.Click += (_, _) => DeleteLayout();
        menu.Items.Add(activate);
        menu.Items.Add(rename);
        menu.Items.Add(duplicate);
        menu.Items.Add(new Separator());
        menu.Items.Add(delete);
        menu.IsOpen = true;
        eventArgs.Handled = true;
    }

    private void BeginLayoutRename(UIElement anchor, MonitorLayout layout)
    {
        renamingLayout = layout;
        LayoutNameText.Text = layout.Name;
        LayoutRenamePopup.PlacementTarget = anchor;
        LayoutRenamePopup.IsOpen = true;
        _ = Dispatcher.BeginInvoke(() =>
        {
            LayoutNameText.Focus();
            LayoutNameText.SelectAll();
        });
    }

    private void LayoutNameText_KeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Escape)
        {
            LayoutRenamePopup.IsOpen = false;
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.Key != Key.Enter || viewModel is null || renamingLayout is null)
        {
            return;
        }

        eventArgs.Handled = true;
        LayoutRenamePopup.IsOpen = false;
        if (string.Equals(LayoutNameText.Text.Trim(), renamingLayout.Name, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            viewModel.EditLayout(renamingLayout.Id);
            viewModel.RenameSelectedLayout(LayoutNameText.Text);
        }
        catch (Exception exception)
        {
            viewModel.StatusMessage = exception.Message;
        }

        RefreshEditor();
    }

    private void DeleteLayout()
    {
        if (viewModel is null)
        {
            return;
        }

        try
        {
            var deleted = viewModel.DeleteSelectedLayout();
            RefreshEditor();
            if (deleted is not null)
            {
                viewModel.ShowToast($"Layout «{deleted.Name}» gelöscht.", () =>
                {
                    viewModel.RestoreLayout(deleted);
                    RefreshEditor();
                });
            }
        }
        catch (Exception exception)
        {
            viewModel.StatusMessage = exception.Message;
        }
    }

    private void AddLayoutMenu_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = eventArgs;
        if (sender is System.Windows.Controls.Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private void AddEmptyLayout_Click(object sender, RoutedEventArgs eventArgs)
    {
        viewModel?.AddEmptyLayout();
        RefreshEditor();
    }

    private void AddLayoutFromTemplate_Click(object sender, RoutedEventArgs eventArgs)
    {
        viewModel?.AddLayout();
        RefreshEditor();
        TemplatePopup.IsOpen = true;
    }

    private void DuplicateLayout_Click(object sender, RoutedEventArgs eventArgs)
    {
        viewModel?.DuplicateSelectedLayout();
        RefreshEditor();
    }

    private void TemplateMenu_Click(object sender, RoutedEventArgs eventArgs) => TemplatePopup.IsOpen = !TemplatePopup.IsOpen;

    private void Template_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel?.Editor is null ||
            sender is not System.Windows.Controls.Button { DataContext: LayoutSuggestion suggestion })
        {
            return;
        }

        TemplatePopup.IsOpen = false;
        viewModel.Editor.ApplyTemplate(suggestion.Template);
        viewModel.StatusMessage = $"Vorlage «{suggestion.Name}» übernommen";
        RefreshEditor();
        viewModel.ShowToast($"Vorlage «{suggestion.Name}» übernommen.", () =>
        {
            viewModel.Editor?.Undo();
            RefreshEditor();
        });
    }

    private void UndoZoneChange_Click(object sender, RoutedEventArgs eventArgs) => UndoZoneChange();

    private void RedoZoneChange_Click(object sender, RoutedEventArgs eventArgs) => RedoZoneChange();

    private void UndoZoneChange()
    {
        if (viewModel?.Editor is { } editor && editor.Undo())
        {
            viewModel.StatusMessage = "Letzte Änderung am Layout zurückgenommen";
            RefreshEditor();
        }
    }

    private void RedoZoneChange()
    {
        if (viewModel?.Editor is { } editor && editor.Redo())
        {
            viewModel.StatusMessage = "Änderung am Layout wiederhergestellt";
            RefreshEditor();
        }
    }

    private void EditorCanvas_DragStarted(object sender, EventArgs eventArgs) => viewModel?.Editor?.BeginInteractiveChange();

    private void EditorCanvas_DragEnded(object sender, EventArgs eventArgs) => viewModel?.Editor?.EndInteractiveChange();

    private void AddZone_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel?.Editor is { } editor && !editor.AddZone())
        {
            viewModel.StatusMessage = "Keine freie rechteckige Fläche für eine weitere Zone vorhanden";
        }
        else if (viewModel is not null)
        {
            viewModel.StatusMessage = "Neue Zone in der grössten freien Fläche erstellt";
        }

        RefreshEditor();
    }

    private void DeleteZone_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel?.Editor?.SelectedZone is { } zone)
        {
            DeleteZone(zone.Id);
        }
    }

    private void DeleteZone(Guid zoneId)
    {
        var editor = viewModel?.Editor;
        if (viewModel is null || editor is null)
        {
            return;
        }

        var name = editor.Zones.FirstOrDefault(zone => zone.Id == zoneId)?.Name ?? "Zone";
        if (!editor.DeleteZone(zoneId))
        {
            viewModel.StatusMessage = "Mindestens eine Zone ist erforderlich";
            return;
        }

        RefreshEditor();
        viewModel.ShowToast($"Zone «{name}» entfernt.", () =>
        {
            viewModel.Editor?.Undo();
            RefreshEditor();
        });
    }

    private void EditorCanvas_ZoneSelected(object sender, ZoneSelectedEventArgs eventArgs)
    {
        viewModel?.Editor?.SelectZone(eventArgs.ZoneId);
        RefreshEditor();
    }

    private void EditorCanvas_ZoneChanged(object sender, ZoneChangedEventArgs eventArgs)
    {
        viewModel?.Editor?.MoveOrResizeZones(eventArgs.SelectedZoneId, eventArgs.ChangedBounds);
        RefreshEditor();
    }

    private void EditorCanvas_ZoneDeleteRequested(object sender, ZoneSelectedEventArgs eventArgs) => DeleteZone(eventArgs.ZoneId);

    private void EditorCanvas_ZoneRenameRequested(object sender, ZoneSelectedEventArgs eventArgs) => BeginZoneRename(eventArgs.ZoneId);

    private void BeginZoneRename(Guid zoneId)
    {
        var zone = viewModel?.Editor?.Zones.FirstOrDefault(candidate => candidate.Id == zoneId);
        var rect = EditorCanvas.GetZoneLabelRect(zoneId);
        if (zone is null || rect is not { } area)
        {
            return;
        }

        renamingZoneId = zoneId;
        RenameTextBox.Text = zone.Name;
        RenameTextBox.Width = area.Width;
        Canvas.SetLeft(RenameTextBox, area.X);
        Canvas.SetTop(RenameTextBox, area.Y);
        RenameOverlay.Visibility = Visibility.Visible;
        _ = Dispatcher.BeginInvoke(() =>
        {
            RenameTextBox.Focus();
            RenameTextBox.SelectAll();
        });
    }

    private void RenameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Enter)
        {
            CommitZoneRename();
            eventArgs.Handled = true;
        }
        else if (eventArgs.Key == Key.Escape)
        {
            renamingZoneId = null;
            RenameOverlay.Visibility = Visibility.Collapsed;
            EditorCanvas.Focus();
            eventArgs.Handled = true;
        }
    }

    private void RenameTextBox_LostFocus(object sender, KeyboardFocusChangedEventArgs eventArgs) => CommitZoneRename();

    private void CommitZoneRename()
    {
        if (renamingZoneId is { } zoneId && viewModel?.Editor is { } editor && RenameTextBox.Text.Trim().Length > 0)
        {
            editor.RenameZone(zoneId, RenameTextBox.Text.Trim());
        }

        renamingZoneId = null;
        RenameOverlay.Visibility = Visibility.Collapsed;
        RefreshEditor();
    }

    private void EditorCanvas_ZoneContextMenuRequested(object sender, ZoneContextMenuEventArgs eventArgs)
    {
        var menu = BuildZoneContextMenu(eventArgs.ZoneId, () => BeginZoneRename(eventArgs.ZoneId));
        if (menu is null)
        {
            return;
        }

        menu.PlacementTarget = EditorCanvas;
        menu.Placement = PlacementMode.RelativePoint;
        menu.HorizontalOffset = eventArgs.Position.X;
        menu.VerticalOffset = eventArgs.Position.Y;
        menu.IsOpen = true;
    }

    /// <summary>Das Kontextmenue einer Zone; Fenster- und Vollbild-Editor teilen es sich.</summary>
    internal ContextMenu? BuildZoneContextMenu(Guid zoneId, Action rename)
    {
        var editor = viewModel?.Editor;
        if (viewModel is null || editor is null || editor.Zones.All(zone => zone.Id != zoneId))
        {
            return null;
        }

        var menu = new ContextMenu();
        var startZone = new MenuItem { Header = editor.StartZoneId == zoneId ? "Startzone aufheben" : "Als Startzone festlegen" };
        startZone.Click += (_, _) => MoveStartZone(zoneId);
        menu.Items.Add(startZone);
        var renameItem = new MenuItem { Header = "Umbenennen", InputGestureText = "Doppelklick" };
        renameItem.Click += (_, _) => rename();
        menu.Items.Add(renameItem);
        foreach (var neighbour in editor.MergeableNeighbours(zoneId))
        {
            var number = editor.Zones.ToList().FindIndex(zone => zone.Id == neighbour.Id) + 1;
            var merge = new MenuItem { Header = $"Mit Zone {number} verbinden" };
            merge.Click += (_, _) =>
            {
                if (editor.MergeZones(zoneId, neighbour.Id))
                {
                    RefreshEditor();
                    viewModel.ShowToast($"Zonen mit «{neighbour.Name}» verbunden.", () =>
                    {
                        viewModel.Editor?.Undo();
                        RefreshEditor();
                    });
                }
            };
            menu.Items.Add(merge);
        }

        menu.Items.Add(new Separator());
        var delete = new MenuItem { Header = "Zone entfernen", InputGestureText = "Entf", IsEnabled = editor.Zones.Count > 1 };
        delete.SetResourceReference(ForegroundProperty, "DangerBrush");
        delete.Click += (_, _) => DeleteZone(zoneId);
        menu.Items.Add(delete);
        return menu;
    }

    /// <summary>
    /// Setzt die Startzone eines Layouts um. Es gibt hoechstens eine je Layout: die alte verliert das
    /// Merkmal. Der Toast nennt beide Zonen und stellt den vorherigen Stand wieder her.
    /// </summary>
    private void MoveStartZone(Guid zoneId)
    {
        if (viewModel?.Editor is not { } editor)
        {
            return;
        }

        var previous = editor.StartZoneId;
        editor.ToggleStartZone(zoneId);
        RefreshEditor();
        var name = editor.Zones.FirstOrDefault(zone => zone.Id == zoneId)?.Name ?? "Zone";
        var text = editor.StartZoneId == zoneId
            ? previous is Guid old && editor.Zones.FirstOrDefault(zone => zone.Id == old) is { } oldZone
                ? $"«{name}» ist jetzt die Startzone statt «{oldZone.Name}»."
                : $"«{name}» ist jetzt die Startzone."
            : $"«{name}» ist keine Startzone mehr.";
        viewModel.ShowToast(text, () =>
        {
            if (viewModel.Editor is { } current)
            {
                current.SetStartZone(previous);
                RefreshEditor();
            }
        });
    }

    private void ZoneValues_StartZoneToggled(object sender, Guid zoneId)
    {
        _ = sender;
        MoveStartZone(zoneId);
    }

    private void ZoneValues_ValuesApplied(object sender, EventArgs eventArgs)
    {
        var editor = viewModel?.Editor;
        EditorCanvas.Zones = editor?.Zones ?? [];
        EditorCanvas.StartZoneId = editor?.StartZoneId;
        EditorCanvas.InvalidateVisual();
        ValidationText.Text = editor?.ValidationMessage ?? string.Empty;
        fullscreenEditor?.RefreshFromEditor();
    }

    private void DrawOnMonitor_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel?.Editor is null || viewModel.SelectedMonitor is null)
        {
            return;
        }

        if (!viewModel.SelectedMonitor.IsConnected)
        {
            viewModel.StatusMessage = "Dieser Monitor ist nicht verbunden; zeichnen geht nur auf einem angeschlossenen Monitor.";
            return;
        }

        if (fullscreenEditor is { IsVisible: true })
        {
            fullscreenEditor.Activate();
            return;
        }

        fullscreenEditor = new FullscreenZoneEditorWindow(this, viewModel);
        fullscreenEditor.Closed += (_, _) =>
        {
            fullscreenEditor = null;
            RefreshEditor();
        };
        fullscreenEditor.Show();
    }

    /// <summary>Ob gerade auf dem Monitor gezeichnet wird; der Controller zeigt dann keine Overlays.</summary>
    public bool IsFullscreenEditorOpen => fullscreenEditor is { IsVisible: true };

    private void RefreshEditor()
    {
        var editor = viewModel?.Editor;
        EditorCanvas.Zones = editor?.Zones ?? [];
        EditorCanvas.SelectedZoneId = editor?.SelectedZone?.Id;
        EditorCanvas.StartZoneId = editor?.StartZoneId;
        AddZoneButton.IsEnabled = editor is not null;
        DeleteZoneButton.IsEnabled = editor?.SelectedZone is not null && editor.Zones.Count > 1;
        var monitor = viewModel?.SelectedMonitor?.Live;
        var width = monitor?.WorkArea.Width ?? 1;
        var height = monitor?.WorkArea.Height ?? 1;
        if (monitor is not null)
        {
            EditorCanvas.MonitorAspectRatio = (double)width / height;
            EditorCanvas.MonitorPixelWidth = width;
            EditorCanvas.MonitorPixelHeight = height;
            EditorCanvas.MagnetThresholdPixels = viewModel?.Settings.MagnetThresholdPixels ?? 10;
        }

        RefreshScalingPage();
        RefreshHighlightPreview();
        ZoneValues.Attach(editor, width, height);
        ValidationText.Text = editor?.ValidationMessage ?? string.Empty;
        EditorCanvas.InvalidateVisual();
        fullscreenEditor?.RefreshFromEditor();
    }

    private void RefreshHighlightPreview()
    {
        var value = viewModel?.Settings.HighlightColor;
        var effective = string.IsNullOrWhiteSpace(value) ? viewModel?.Settings.OverlayColor : value;
        if (TryParseRgb(effective ?? string.Empty, out var red, out var green, out var blue))
        {
            HighlightColorPreview.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)red, (byte)green, (byte)blue));
        }
    }

    // ----------------------------------------------------------------- Fenster zuordnen

    private void AppRuleAdd_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (viewModel is null)
        {
            return;
        }

        ShowPage(NavigationPage.Rules);
        try
        {
            var dialog = new AssignWindowDialog(viewModel, AssignWindowDialog.Mode.Assign) { Owner = this };
            dialog.RemoveRuleRequested += RemoveRuleWithToast;
            if (dialog.ShowDialog() != true || dialog.SelectedProcessName is not { Length: > 0 } process)
            {
                return;
            }

            if (dialog.SelectedLayoutId is { } layoutId && dialog.SelectedZoneId is { } zoneId)
            {
                var rule = viewModel.AppRules.AddRule(process, dialog.SelectedEvent, layoutId, zoneId);
                viewModel.StatusMessage = $"{rule.DisplayName} → {viewModel.AppRules.DescribeTarget(rule)} zugeordnet";
                if (dialog.OpenDetails && viewModel.AppRules.RuleItems.FirstOrDefault(item => item.Id == rule.Id) is { } item)
                {
                    viewModel.AppRules.ToggleExpanded(item);
                }
            }
        }
        catch (Exception exception)
        {
            viewModel.StatusMessage = $"Die laufenden Programme konnten nicht gelesen werden: {exception.Message}";
        }
    }

    private void AppRuleToggleDetail_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is FrameworkElement { DataContext: AppRuleListItem item })
        {
            viewModel?.AppRules.ToggleExpanded(item);
        }
    }

    private void AppRuleEnabled_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is System.Windows.Controls.CheckBox { DataContext: AppRuleListItem item } toggle)
        {
            viewModel?.AppRules.SetEnabled(item, toggle.IsChecked == true);
        }
    }

    private void AppRuleDone_Click(object sender, RoutedEventArgs eventArgs) => viewModel?.AppRules.CollapseAll();

    private void AppRuleDelete_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel?.AppRules.SelectedRule is { } rule)
        {
            RemoveRuleWithToast(rule);
        }
    }

    private void RemoveRuleWithToast(AppRule rule)
    {
        if (viewModel is null)
        {
            return;
        }

        var index = viewModel.AppRules.RemoveRule(rule);
        if (index < 0)
        {
            return;
        }

        viewModel.ShowToast($"Zuordnung «{rule.DisplayName}» entfernt.", () => viewModel.AppRules.RestoreRule(rule, index));
    }

    private void AppRuleBrowse_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (viewModel is null)
        {
            return;
        }

        try
        {
            var dialog = new AssignWindowDialog(viewModel, AssignWindowDialog.Mode.PickProgram) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.SelectedProcessName is { Length: > 0 } process)
            {
                viewModel.AppRules.ProcessPath = process;
            }
        }
        catch (Exception exception)
        {
            viewModel.StatusMessage = $"Die laufenden Programme konnten nicht gelesen werden: {exception.Message}";
        }
    }

    // ----------------------------------------------------------------- In Ruhe lassen

    private void AppExclusionAdd_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (viewModel is null)
        {
            return;
        }

        ShowPage(NavigationPage.Exclusions);
        try
        {
            var dialog = new AssignWindowDialog(viewModel, AssignWindowDialog.Mode.Exclude) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.SelectedProcessName is { Length: > 0 } process)
            {
                var exclusion = viewModel.AppExclusions.AddExclusion(process);
                viewModel.StatusMessage = $"«{exclusion.DisplayName}» wird in Ruhe gelassen";
            }
        }
        catch (Exception exception)
        {
            viewModel.StatusMessage = $"Die laufenden Programme konnten nicht gelesen werden: {exception.Message}";
        }
    }

    private void AppExclusionToggleDetail_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is FrameworkElement { DataContext: AppExclusionListItem item })
        {
            viewModel?.AppExclusions.ToggleExpanded(item);
        }
    }

    private void AppExclusionEnabled_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is System.Windows.Controls.CheckBox { DataContext: AppExclusionListItem item } toggle)
        {
            viewModel?.AppExclusions.SetEnabled(item, toggle.IsChecked == true);
        }
    }

    private void AppExclusionDone_Click(object sender, RoutedEventArgs eventArgs) => viewModel?.AppExclusions.CollapseAll();

    private void AppExclusionDelete_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel?.AppExclusions.SelectedExclusion is not { } exclusion)
        {
            return;
        }

        var index = viewModel.AppExclusions.RemoveExclusion(exclusion);
        if (index < 0)
        {
            return;
        }

        viewModel.ShowToast($"«{exclusion.DisplayName}» rastet wieder ein.", () => viewModel.AppExclusions.RestoreExclusion(exclusion, index));
    }

    // ----------------------------------------------------------------- Verhalten

    private void ForgetWindowPositions_Click(object sender, RoutedEventArgs eventArgs) => viewModel?.ForgetWindowPositions();

    private void OverlayColorPicker_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        var selectedColor = pickOverlayColor(OverlayColorText.Text);
        if (selectedColor is null)
        {
            return;
        }

        OverlayColorText.Text = selectedColor;
        OverlayColorText.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
    }

    private void HighlightColorPicker_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        var current = string.IsNullOrWhiteSpace(HighlightColorText.Text) ? OverlayColorText.Text : HighlightColorText.Text;
        var selectedColor = pickOverlayColor(current);
        if (selectedColor is null)
        {
            return;
        }

        HighlightColorText.Text = selectedColor;
        HighlightColorText.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        _ = sender;
        if (Keyboard.Modifiers == ModifierKeys.Control &&
            eventArgs.Key is Key.Z or Key.Y &&
            ReferenceEquals(NavigationTabs.SelectedItem, LayoutsTab) &&
            Keyboard.FocusedElement is not System.Windows.Controls.TextBox)
        {
            if (eventArgs.Key == Key.Z)
            {
                UndoZoneChange();
            }
            else
            {
                RedoZoneChange();
            }

            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.Key is not (Key.Left or Key.Right))
        {
            return;
        }

        // Nur der fokussierte Regler reagiert auf Pfeiltasten. Frueher gewann der Regler unter dem
        // Mauszeiger und verstellte sich, waehrend im Textfeld daneben der Cursor bewegt werden sollte.
        var sliders = new[] { OverlayOpacitySlider, ZoneGapSlider, MagnetThresholdSlider };
        var target = Keyboard.FocusedElement as Slider;
        if (target is null || !sliders.Contains(target))
        {
            return;
        }

        var direction = eventArgs.Key == Key.Right ? 1 : -1;
        var step = SliderArrowStep(target.Maximum - target.Minimum);
        target.Value = Math.Clamp(target.Value + direction * step, target.Minimum, target.Maximum);
        eventArgs.Handled = true;
    }

    private static double SliderArrowStep(double range) => range switch
    {
        <= 100 => 1,
        <= 2500 => 25,
        _ => 100
    };

    // ----------------------------------------------------------------- Programm

    private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (viewModel is not null && System.Windows.Application.Current is App application)
        {
            application.ApplyTheme(viewModel.Settings.ThemeMode);
        }
    }

    private async void ExportConfiguration_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        var dialog = new SaveFileDialog
        {
            Title = "Vollständige Konfiguration exportieren",
            Filter = "Zone Manager Vollbackup (*.swz.json)|*.swz.json|JSON-Dateien (*.json)|*.json",
            DefaultExt = ".swz.json",
            AddExtension = true,
            FileName = $"ZoneManager-Vollbackup-{DateTime.Now:yyyy-MM-dd-HHmm}.swz.json"
        };
        if (dialog.ShowDialog(this) == true && ExportConfigurationRequested is { } export)
        {
            await RunConfigurationTransferAsync(() => export(dialog.FileName));
        }
    }

    private async void ImportConfiguration_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        var dialog = new OpenFileDialog
        {
            Title = "Vollständige Konfiguration importieren",
            Filter = "Zone Manager Vollbackup (*.swz.json)|*.swz.json|JSON-Dateien (*.json)|*.json",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true && ImportConfigurationRequested is { } import)
        {
            await RunConfigurationTransferAsync(() => import(dialog.FileName));
        }
    }

    private async Task RunConfigurationTransferAsync(Func<Task> action)
    {
        ExportConfigurationButton.IsEnabled = false;
        ImportConfigurationButton.IsEnabled = false;
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            if (viewModel is not null)
            {
                viewModel.StatusMessage = exception.Message;
            }
        }
        finally
        {
            ExportConfigurationButton.IsEnabled = true;
            ImportConfigurationButton.IsEnabled = true;
        }
    }

    private void RestoreBackup_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is FrameworkElement { DataContext: BackupListItem item })
        {
            viewModel?.RestoreBackup(item);
        }
    }

    private void ResetSettings_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (viewModel is null)
        {
            return;
        }

        var previous = viewModel.Settings.CreateSettings();
        viewModel.Settings.ResetToDefaults();
        viewModel.StatusMessage = "Einstellungen auf die Voreinstellung zurückgesetzt";
        RefreshEditor();
        viewModel.ShowToast("Alle Einstellungen zurückgesetzt.", () =>
        {
            viewModel.Settings.Apply(previous);
            RefreshEditor();
        });
    }

    private void ResumeSnapping_Click(object sender, RoutedEventArgs eventArgs) => viewModel?.ResumeSnapping();

    private void HelperWizard_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel is null)
        {
            return;
        }

        var wizard = new HelperWizardWindow(viewModel) { Owner = this };
        wizard.ShowDialog();
    }

    private void InstallDisplayDriver_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (viewModel is null)
        {
            return;
        }

        if (System.Windows.MessageBox.Show(
                "Der Vollbildzonen-Treiber «Virtual Display Driver» wird eingerichtet. Er legt einen zusätzlichen, "
                    + "virtuellen Monitor an, den Zone Manager nur dann anhängt, wenn ein Fenster in einer "
                    + "Vollbildzone liegt. Windows fragt einmal nach Administratorrechten.",
                "Vollbildzonen-Treiber installieren",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information) != MessageBoxResult.OK)
        {
            return;
        }

        viewModel.InstallDisplayDriver();
    }

    private void RemoveDisplayDriver_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (viewModel is null)
        {
            return;
        }

        if (System.Windows.MessageBox.Show(
                "Der Vollbildzonen-Treiber wird samt virtuellem Monitor entfernt. Vollbildzonen bleiben danach "
                    + "ausgeschaltet, bis er erneut installiert wird. Windows fragt einmal nach Administratorrechten.",
                "Vollbildzonen-Treiber entfernen",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information) != MessageBoxResult.OK)
        {
            return;
        }

        viewModel.RemoveDisplayDriver();
    }

    private void CheckForUpdates_Click(object sender, RoutedEventArgs eventArgs) => viewModel?.CheckForUpdates();

    private void InstallUpdate_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (viewModel is null)
        {
            return;
        }

        if (System.Windows.MessageBox.Show(
                "Die neue Programmdatei wird geladen und an die Stelle der laufenden gelegt. "
                    + "Danach startet das Programm neu; Einstellungen und Layouts bleiben erhalten.",
                "Update installieren",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information) != MessageBoxResult.OK)
        {
            return;
        }

        viewModel.InstallUpdate();
    }

    // ----------------------------------------------------------------- Reihenfolge per Ziehen

    private System.Windows.Point reorderStart;
    private object? reorderItem;

    private void LayoutTabs_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs eventArgs)
    {
        reorderStart = eventArgs.GetPosition(this);
        reorderItem = ItemUnderMouse<MonitorLayout>(eventArgs);
    }

    private void LayoutTabs_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs eventArgs)
    {
        if (BeginReorderDrag(sender, eventArgs, reorderItem as MonitorLayout))
        {
            reorderItem = null;
        }
    }

    private void LayoutTabs_DragOver(object sender, System.Windows.DragEventArgs eventArgs) =>
        AllowReorderDrop<MonitorLayout>(eventArgs);

    private void LayoutTabs_Drop(object sender, System.Windows.DragEventArgs eventArgs)
    {
        _ = sender;
        if (viewModel is null ||
            eventArgs.Data.GetData(typeof(MonitorLayout)) is not MonitorLayout moved ||
            ItemUnderMouse<MonitorLayout>(eventArgs) is not { } target ||
            moved.Id == target.Id)
        {
            return;
        }

        viewModel.MoveLayoutTo(moved.Id, target.Id);
        eventArgs.Handled = true;
    }

    private void MonitorTabs_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs eventArgs)
    {
        reorderStart = eventArgs.GetPosition(this);
        reorderItem = ItemUnderMouse<MonitorChoice>(eventArgs);
    }

    private void MonitorTabs_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs eventArgs)
    {
        if (BeginReorderDrag(sender, eventArgs, reorderItem as MonitorChoice))
        {
            reorderItem = null;
        }
    }

    private void MonitorTabs_DragOver(object sender, System.Windows.DragEventArgs eventArgs) =>
        AllowReorderDrop<MonitorChoice>(eventArgs);

    private void MonitorTabs_Drop(object sender, System.Windows.DragEventArgs eventArgs)
    {
        _ = sender;
        if (viewModel is null ||
            eventArgs.Data.GetData(typeof(MonitorChoice)) is not MonitorChoice moved ||
            ItemUnderMouse<MonitorChoice>(eventArgs) is not { } target ||
            ReferenceEquals(moved, target))
        {
            return;
        }

        viewModel.MoveMonitorTo(moved, target);
        eventArgs.Handled = true;
    }

    /// <summary>
    /// Startet ein Ziehen erst, wenn die Maus die Systemschwelle ueberschritten hat. Ohne das wuerde
    /// jeder Klick auf einen Tab als Ziehen gelten und der Tabwechsel ausbleiben.
    /// </summary>
    private bool BeginReorderDrag<T>(object source, System.Windows.Input.MouseEventArgs eventArgs, T? item)
        where T : class
    {
        if (item is null || eventArgs.LeftButton != MouseButtonState.Pressed)
        {
            return false;
        }

        var position = eventArgs.GetPosition(this);
        if (Math.Abs(position.X - reorderStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - reorderStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return false;
        }

        System.Windows.DragDrop.DoDragDrop((DependencyObject)source, new System.Windows.DataObject(typeof(T), item), System.Windows.DragDropEffects.Move);
        return true;
    }

    private static void AllowReorderDrop<T>(System.Windows.DragEventArgs eventArgs)
    {
        eventArgs.Effects = eventArgs.Data.GetDataPresent(typeof(T)) ? System.Windows.DragDropEffects.Move : System.Windows.DragDropEffects.None;
        eventArgs.Handled = true;
    }

    private static T? ItemUnderMouse<T>(RoutedEventArgs eventArgs)
        where T : class
    {
        var element = eventArgs.OriginalSource as DependencyObject;
        while (element is not null)
        {
            if (element is FrameworkElement { DataContext: T item })
            {
                return item;
            }

            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    // ----------------------------------------------------------------- System, Protokoll, Diagnose

    private void CheckDisplayDriver_Click(object sender, RoutedEventArgs eventArgs) => viewModel?.CheckDisplayDriver();

    private void RemoveCertificate_Click(object sender, RoutedEventArgs eventArgs) => viewModel?.RemoveCertificate();

    private void ReleaseElevation_Click(object sender, RoutedEventArgs eventArgs) => viewModel?.ReleaseElevation();

    private void RunDiagnostics_Click(object sender, RoutedEventArgs eventArgs) => viewModel?.RunDiagnostics();

    private void OpenLogFolder_Click(object sender, RoutedEventArgs eventArgs) => viewModel?.OpenLogFolder();

    private void OpenSettingsFolder_Click(object sender, RoutedEventArgs eventArgs) => viewModel?.OpenSettingsFolder();

    private void ClearLog_Click(object sender, RoutedEventArgs eventArgs) => viewModel?.ClearLog();

    private void UpdateSource_Click(object sender, RoutedEventArgs eventArgs) => viewModel?.ToggleUpdateSource();

    private void OpenProjectPage_Click(object sender, RoutedEventArgs eventArgs) =>
        OpenInBrowser($"https://github.com/{MainViewModel.UpdateSourceRepository}");

    private void OpenLicense_Click(object sender, RoutedEventArgs eventArgs) =>
        OpenInBrowser($"https://github.com/{MainViewModel.UpdateSourceRepository}/blob/main/LICENSE");

    private void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            if (viewModel is not null)
            {
                viewModel.StatusMessage = $"Die Seite liess sich nicht öffnen: {exception.Message}";
            }
        }
    }

    // ----------------------------------------------------------------- Programme ohne Vollbildzone

    private void FullscreenExclusionAdd_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (viewModel is null)
        {
            return;
        }

        try
        {
            var dialog = new AssignWindowDialog(viewModel, AssignWindowDialog.Mode.PickProgram) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.SelectedProcessName is { Length: > 0 } process)
            {
                viewModel.Settings.AddFullscreenExclusion(process);
            }
        }
        catch (Exception exception)
        {
            viewModel.StatusMessage = $"Die laufenden Programme konnten nicht gelesen werden: {exception.Message}";
        }
    }

    private void FullscreenExclusionRemove_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = eventArgs;
        if (viewModel is not null && sender is FrameworkElement { DataContext: string program })
        {
            viewModel.Settings.RemoveFullscreenExclusion(program);
            viewModel.ShowToast(
                $"«{program}» bekommt wieder eine Vollbildzone.",
                () => viewModel.Settings.AddFullscreenExclusion(program));
        }
    }

    /// <summary>Der Verweis «Jetzt einrichten» aus dem Werte-Panel fuehrt auf «System &amp; Rechte».</summary>
    private void ZoneValues_FullscreenZoneSetupRequested(object sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        ShowPage(NavigationPage.System);
    }

    // ----------------------------------------------------------------- Farbwahl

    private string? PickOverlayColorWithDialog(string currentColor)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            AllowFullOpen = true,
            AnyColor = true,
            FullOpen = true
        };
        if (TryParseRgb(currentColor, out var red, out var green, out var blue))
        {
            dialog.Color = System.Drawing.Color.FromArgb(red, green, blue);
        }

        var ownerHandle = new WindowInteropHelper(this).Handle;
        var result = ownerHandle == nint.Zero
            ? dialog.ShowDialog()
            : dialog.ShowDialog(new DialogOwner(ownerHandle));
        return result == System.Windows.Forms.DialogResult.OK
            ? $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}"
            : null;
    }

    private static bool TryParseRgb(string value, out int red, out int green, out int blue)
    {
        red = 0;
        green = 0;
        blue = 0;
        if (value.Length != 7 || value[0] != '#' ||
            !int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        red = (rgb >> 16) & 0xFF;
        green = (rgb >> 8) & 0xFF;
        blue = rgb & 0xFF;
        return true;
    }

    private sealed class DialogOwner(nint handle) : System.Windows.Forms.IWin32Window
    {
        public nint Handle { get; } = handle;
    }
}
