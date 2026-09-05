using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using SnapZones.App.Controls;
using SnapZones.App.ViewModels;
using SnapZones.Core.Editor;
using SnapZones.Core.Geometry;
using SnapZones.Windows.Windows;

namespace SnapZones.App.Views;

/// <summary>
/// Der Editor in echter Groesse: ein randloses Fenster ueber der Arbeitsflaeche des gewaehlten Monitors.
/// Er teilt sich den <see cref="LayoutEditorViewModel"/> mit dem Hauptfenster; jede Aenderung ist wie dort
/// sofort gespeichert, Rueckgaengig und Wiederholen arbeiten auf demselben Verlauf.
/// </summary>
public partial class FullscreenZoneEditorWindow : Window
{
    private readonly MainWindow owner;
    private readonly MainViewModel viewModel;
    private Guid? renamingZoneId;

    public FullscreenZoneEditorWindow(MainWindow owner, MainViewModel viewModel)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => PositionOnMonitor();
        Loaded += (_, _) =>
        {
            RefreshFromEditor();
            Canvas.Focus();
        };
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Closed += (_, _) => viewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(MainViewModel.Editor) or nameof(MainViewModel.SelectedLayout))
        {
            RefreshFromEditor();
        }
    }

    private void PositionOnMonitor()
    {
        var monitor = viewModel.SelectedMonitor?.Live;
        if (monitor is null)
        {
            Close();
            return;
        }

        OverlayWindowNative.Position(
            new WindowInteropHelper(this).Handle,
            new PixelRect(monitor.WorkArea.X, monitor.WorkArea.Y, monitor.WorkArea.Width, monitor.WorkArea.Height));
    }

    /// <summary>Zieht Zeichenflaeche, Titel und Werte-Panel auf den Stand des Editors.</summary>
    public void RefreshFromEditor()
    {
        var editor = viewModel.Editor;
        var monitor = viewModel.SelectedMonitor;
        Canvas.Zones = editor?.Zones ?? [];
        Canvas.SelectedZoneId = editor?.SelectedZone?.Id;
        Canvas.MainZoneId = editor?.MainZoneId;
        Canvas.MonitorPixelWidth = monitor?.Live.WorkArea.Width ?? 1;
        Canvas.MonitorPixelHeight = monitor?.Live.WorkArea.Height ?? 1;
        Canvas.MagnetThresholdPixels = viewModel.Settings.MagnetThresholdPixels;
        Canvas.ZoneGapPixels = viewModel.Settings.ZoneGap;
        Canvas.InvalidateVisual();
        TitleText.Text = $"{monitor?.UserFacingName} · {viewModel.SelectedLayout?.Name}";
        UndoButton.IsEnabled = editor?.CanUndo ?? false;
        RedoButton.IsEnabled = editor?.CanRedo ?? false;
        TemplateSuggestions.ItemsSource = monitor?.LayoutSuggestions;
        ZoneValues.Attach(editor, Canvas.MonitorPixelWidth, Canvas.MonitorPixelHeight);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Escape && Keyboard.FocusedElement is not System.Windows.Controls.TextBox)
        {
            Close();
            eventArgs.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && Keyboard.FocusedElement is not System.Windows.Controls.TextBox)
        {
            if (eventArgs.Key == Key.Z)
            {
                Undo_Click(sender, eventArgs);
                eventArgs.Handled = true;
            }
            else if (eventArgs.Key == Key.Y)
            {
                Redo_Click(sender, eventArgs);
                eventArgs.Handled = true;
            }
        }
    }

    private void Canvas_ZoneSelected(object sender, ZoneSelectedEventArgs eventArgs)
    {
        viewModel.Editor?.SelectZone(eventArgs.ZoneId);
        RefreshFromEditor();
    }

    private void Canvas_ZoneChanged(object sender, ZoneChangedEventArgs eventArgs)
    {
        viewModel.Editor?.MoveOrResizeZones(eventArgs.SelectedZoneId, eventArgs.ChangedBounds);
        RefreshFromEditor();
    }

    private void Canvas_DragStarted(object sender, EventArgs eventArgs) => viewModel.Editor?.BeginInteractiveChange();

    private void Canvas_DragEnded(object sender, EventArgs eventArgs) => viewModel.Editor?.EndInteractiveChange();

    private void Canvas_ZoneDeleteRequested(object sender, ZoneSelectedEventArgs eventArgs) => DeleteZone(eventArgs.ZoneId);

    private void DeleteZone(Guid zoneId)
    {
        var editor = viewModel.Editor;
        if (editor is null)
        {
            return;
        }

        var name = editor.Zones.FirstOrDefault(zone => zone.Id == zoneId)?.Name ?? "Zone";
        if (!editor.DeleteZone(zoneId))
        {
            viewModel.StatusMessage = "Mindestens eine Zone ist erforderlich";
            return;
        }

        RefreshFromEditor();
        viewModel.ShowToast($"Zone «{name}» entfernt.", () =>
        {
            viewModel.Editor?.Undo();
            RefreshFromEditor();
        });
    }

    private void Canvas_ZoneRenameRequested(object sender, ZoneSelectedEventArgs eventArgs) => BeginRename(eventArgs.ZoneId);

    private void BeginRename(Guid zoneId)
    {
        var zone = viewModel.Editor?.Zones.FirstOrDefault(candidate => candidate.Id == zoneId);
        if (zone is null || Canvas.GetZoneLabelRect(zoneId) is not { } area)
        {
            return;
        }

        renamingZoneId = zoneId;
        RenameTextBox.Text = zone.Name;
        RenameTextBox.Width = Math.Max(160, area.Width);
        System.Windows.Controls.Canvas.SetLeft(RenamePanel, area.X);
        System.Windows.Controls.Canvas.SetTop(RenamePanel, area.Y);
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
            CommitRename();
            eventArgs.Handled = true;
        }
        else if (eventArgs.Key == Key.Escape)
        {
            renamingZoneId = null;
            RenameOverlay.Visibility = Visibility.Collapsed;
            Canvas.Focus();
            eventArgs.Handled = true;
        }
    }

    private void RenameTextBox_LostFocus(object sender, KeyboardFocusChangedEventArgs eventArgs) => CommitRename();

    private void CommitRename()
    {
        if (renamingZoneId is { } zoneId && viewModel.Editor is { } editor && RenameTextBox.Text.Trim().Length > 0)
        {
            editor.RenameZone(zoneId, RenameTextBox.Text.Trim());
        }

        renamingZoneId = null;
        RenameOverlay.Visibility = Visibility.Collapsed;
        RefreshFromEditor();
    }

    private void Canvas_ZoneContextMenuRequested(object sender, ZoneContextMenuEventArgs eventArgs)
    {
        var menu = owner.BuildZoneContextMenu(eventArgs.ZoneId, () => BeginRename(eventArgs.ZoneId));
        if (menu is null)
        {
            return;
        }

        menu.Closed += (_, _) => RefreshFromEditor();
        menu.PlacementTarget = Canvas;
        menu.Placement = PlacementMode.RelativePoint;
        menu.HorizontalOffset = eventArgs.Position.X;
        menu.VerticalOffset = eventArgs.Position.Y;
        menu.IsOpen = true;
    }

    private void AddZone_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel.Editor is { } editor && !editor.AddZone())
        {
            viewModel.StatusMessage = "Keine freie rechteckige Fläche für eine weitere Zone vorhanden";
        }

        RefreshFromEditor();
    }

    private void Template_Click(object sender, RoutedEventArgs eventArgs) => TemplatePopup.IsOpen = !TemplatePopup.IsOpen;

    private void TemplateChoice_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel.Editor is null || sender is not System.Windows.Controls.Button { DataContext: LayoutSuggestion suggestion })
        {
            return;
        }

        TemplatePopup.IsOpen = false;
        viewModel.Editor.ApplyTemplate(suggestion.Template);
        RefreshFromEditor();
        viewModel.ShowToast($"Vorlage «{suggestion.Name}» übernommen.", () =>
        {
            viewModel.Editor?.Undo();
            RefreshFromEditor();
        });
    }

    private void Undo_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel.Editor?.Undo() == true)
        {
            RefreshFromEditor();
        }
    }

    private void Redo_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel.Editor?.Redo() == true)
        {
            RefreshFromEditor();
        }
    }

    private void Values_Click(object sender, RoutedEventArgs eventArgs)
    {
        ZoneValues.Attach(viewModel.Editor, Canvas.MonitorPixelWidth, Canvas.MonitorPixelHeight);
        ValuesPopup.IsOpen = !ValuesPopup.IsOpen;
    }

    private void ZoneValues_ValuesApplied(object sender, EventArgs eventArgs)
    {
        Canvas.Zones = viewModel.Editor?.Zones ?? [];
        Canvas.MainZoneId = viewModel.Editor?.MainZoneId;
        Canvas.InvalidateVisual();
    }

    private void Done_Click(object sender, RoutedEventArgs eventArgs) => Close();
}
