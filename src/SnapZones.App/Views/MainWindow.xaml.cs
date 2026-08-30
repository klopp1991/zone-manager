using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SnapZones.App.Controls;
using SnapZones.App.ViewModels;
using SnapZones.Core.Editor;

namespace SnapZones.App.Views;

public partial class MainWindow : Window
{
    private MainViewModel? viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void AttachViewModel(MainViewModel model)
    {
        viewModel = model;
        DataContext = model;
        model.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName is nameof(MainViewModel.Editor) or nameof(MainViewModel.SelectedMonitor))
            {
                RefreshEditor();
            }
        };
        model.Settings.PropertyChanged += (_, _) => RefreshSafetyStatus();
        RefreshEditor();
        RefreshSafetyStatus();
    }

    private void Save_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel is null)
        {
            return;
        }

        try
        {
            viewModel.Save();
            RefreshEditor();
        }
        catch (Exception exception)
        {
            viewModel.StatusMessage = exception.Message;
        }
    }

    private void Template_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel?.Editor is null || sender is not System.Windows.Controls.Button { Tag: string tag } || !Enum.TryParse<LayoutTemplate>(tag, out var template))
        {
            return;
        }

        viewModel.Editor.ApplyTemplate(template);
        viewModel.StatusMessage = "Vorlage als Entwurf angewendet";
        RefreshEditor();
    }

    private void AddZone_Click(object sender, RoutedEventArgs eventArgs)
    {
        viewModel?.Editor?.AddZone();
        RefreshEditor();
    }

    private void DeleteZone_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel?.Editor is null || viewModel.Editor.Zones.Count <= 1)
        {
            if (viewModel is not null) viewModel.StatusMessage = "Mindestens eine Zone ist erforderlich";
            return;
        }

        viewModel.Editor.DeleteSelected();
        RefreshEditor();
    }

    private void ResetLayout_Click(object sender, RoutedEventArgs eventArgs)
    {
        viewModel?.Editor?.Reset();
        RefreshEditor();
    }

    private void ApplyZoneValues_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel?.Editor?.SelectedZone is null)
        {
            return;
        }

        if (!TryPercent(ZoneXText.Text, out var x) ||
            !TryPercent(ZoneYText.Text, out var y) ||
            !TryPercent(ZoneWidthText.Text, out var width) ||
            !TryPercent(ZoneHeightText.Text, out var height))
        {
            viewModel.StatusMessage = "Koordinaten müssen Zahlen zwischen 0 und 100 sein";
            return;
        }

        viewModel.Editor.UpdateSelectedZone(ZoneNameText.Text, x, y, width, height);
        RefreshEditor();
    }

    private void EditorCanvas_ZoneSelected(object sender, ZoneSelectedEventArgs eventArgs)
    {
        viewModel?.Editor?.SelectZone(eventArgs.ZoneId);
        RefreshEditor();
    }

    private void EditorCanvas_ZoneChanged(object sender, ZoneChangedEventArgs eventArgs)
    {
        viewModel?.Editor?.MoveOrResizeZone(eventArgs.ZoneId, eventArgs.Bounds);
        RefreshEditor();
    }

    private void Monitor_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs) => RefreshEditor();

    private void AddProfile_Click(object sender, RoutedEventArgs eventArgs)
    {
        viewModel?.AddProfile();
        RefreshEditor();
    }

    private void RenameProfile_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel is null)
        {
            return;
        }

        try
        {
            viewModel.RenameSelectedProfile(ProfileNameText.Text);
            viewModel.StatusMessage = "Profilname gespeichert";
        }
        catch (Exception exception)
        {
            viewModel.StatusMessage = exception.Message;
        }
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel is null)
        {
            return;
        }

        var impact = $"Das Profil «{viewModel.SelectedProfile.Name}» und seine Layouts werden gelöscht.";
        if (System.Windows.MessageBox.Show(impact, "Profil löschen", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
        {
            return;
        }

        try
        {
            viewModel.DeleteSelectedProfile();
            RefreshEditor();
        }
        catch (Exception exception)
        {
            viewModel.StatusMessage = exception.Message;
        }
    }

    private void SafetySetting_Changed(object sender, RoutedEventArgs eventArgs) => RefreshSafetyStatus();

    private void RefreshEditor()
    {
        var editor = viewModel?.Editor;
        EditorCanvas.Zones = editor?.Zones ?? [];
        EditorCanvas.SelectedZoneId = editor?.SelectedZone?.Id;
        var monitor = viewModel?.SelectedMonitor?.Live;
        if (monitor is not null)
        {
            EditorCanvas.MonitorAspectRatio = (double)monitor.WorkArea.Width / monitor.WorkArea.Height;
            MonitorTitle.Text = $"{monitor.Identity.FriendlyName}  ·  {monitor.WorkArea.Width} × {monitor.WorkArea.Height}";
        }

        var zone = editor?.SelectedZone;
        ZoneNameText.Text = zone?.Name ?? string.Empty;
        ZoneXText.Text = FormatPercent(zone?.Bounds.X);
        ZoneYText.Text = FormatPercent(zone?.Bounds.Y);
        ZoneWidthText.Text = FormatPercent(zone?.Bounds.Width);
        ZoneHeightText.Text = FormatPercent(zone?.Bounds.Height);
        ValidationText.Text = editor?.ValidationMessage ?? string.Empty;
        EditorCanvas.InvalidateVisual();
    }

    private void RefreshSafetyStatus()
    {
        var enabled = viewModel?.Settings.SnappingEnabled == true;
        SafetyStatus.Text = enabled ? "Snap-Funktion nach Speichern aktiv" : "Snap-Funktion aus";
        SafetyBadge.Background = (System.Windows.Media.Brush)FindResource(enabled ? "AccentSoftBrush" : "WarningSoftBrush");
        SafetyStatus.Foreground = (System.Windows.Media.Brush)FindResource(enabled ? "AccentBrush" : "WarningBrush");
    }

    private static bool TryPercent(string text, out double value)
    {
        var valid = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        return valid && value is >= 0 and <= 100;
    }

    private static string FormatPercent(double? value) => value is null
        ? string.Empty
        : (value.Value * 100).ToString("0.##", CultureInfo.CurrentCulture);
}
