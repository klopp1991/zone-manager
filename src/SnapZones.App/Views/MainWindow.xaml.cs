using System.Globalization;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SnapZones.App.Controls;
using SnapZones.App.ViewModels;
using SnapZones.Core.Editor;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

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
        model.Settings.PropertyChanged += (_, _) =>
        {
            RefreshSafetyStatus();
            RefreshEditor();
        };
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

        var unit = SelectedMeasurementUnit();
        var horizontalMaximum = unit == MeasurementUnit.Percent
            ? 100d
            : viewModel.SelectedMonitor?.Live.WorkArea.Width ?? 1;
        var verticalMaximum = unit == MeasurementUnit.Percent
            ? 100d
            : viewModel.SelectedMonitor?.Live.WorkArea.Height ?? 1;
        if (!TryMeasurement(ZoneLeftText.Text, horizontalMaximum, out var left) ||
            !TryMeasurement(ZoneTopText.Text, verticalMaximum, out var top))
        {
            viewModel.StatusMessage = "Links und Oben liegen ausserhalb der gewählten Einheit";
            return;
        }

        if (SelectedDefinitionMode() == ZoneDefinitionMode.PositionAndSize)
        {
            if (!TryMeasurement(ZoneWidthText.Text, horizontalMaximum, out var width) ||
                !TryMeasurement(ZoneHeightText.Text, verticalMaximum, out var height))
            {
                viewModel.StatusMessage = "Breite und Höhe liegen ausserhalb der gewählten Einheit";
                return;
            }

            viewModel.Editor.UpdateSelectedZoneFromPositionAndSize(
                ZoneNameText.Text, left, top, width, height, unit);
        }
        else
        {
            if (!TryMeasurement(ZoneRightText.Text, horizontalMaximum, out var right) ||
                !TryMeasurement(ZoneBottomText.Text, verticalMaximum, out var bottom))
            {
                viewModel.StatusMessage = "Rechts und Unten liegen ausserhalb der gewählten Einheit";
                return;
            }

            viewModel.Editor.UpdateSelectedZoneFromMargins(
                ZoneNameText.Text, left, top, right, bottom, unit);
        }
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

    private void ZoneInputMode_Changed(object sender, SelectionChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (ZoneLeftText is not null)
        {
            RefreshZoneFields();
        }
    }

    private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (viewModel is not null && System.Windows.Application.Current is App application)
        {
            application.ApplyTheme(viewModel.Settings.ThemeMode);
        }
    }

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

    private void RefreshEditor()
    {
        var editor = viewModel?.Editor;
        EditorCanvas.Zones = editor?.Zones ?? [];
        EditorCanvas.SelectedZoneId = editor?.SelectedZone?.Id;
        var monitor = viewModel?.SelectedMonitor?.Live;
        if (monitor is not null)
        {
            EditorCanvas.MonitorAspectRatio = (double)monitor.WorkArea.Width / monitor.WorkArea.Height;
            EditorCanvas.MonitorPixelWidth = monitor.WorkArea.Width;
            EditorCanvas.MonitorPixelHeight = monitor.WorkArea.Height;
            EditorCanvas.MagnetThresholdPixels = viewModel?.Settings.MagnetThresholdPixels ?? 10;
            MonitorTitle.Text = $"{monitor.Identity.FriendlyName}  ·  {monitor.WorkArea.Width} × {monitor.WorkArea.Height}";
            WindowsScaleText.Text = $"{monitor.Identity.FriendlyName}: {Math.Round(monitor.DpiX / 96d * 100):0} % erkannt";
        }

        RefreshZoneFields();
        ValidationText.Text = editor?.ValidationMessage ?? string.Empty;
        EditorCanvas.InvalidateVisual();
    }

    private void RefreshZoneFields()
    {
        var editor = viewModel?.Editor;
        var zone = editor?.SelectedZone;
        ZoneNameText.Text = zone?.Name ?? string.Empty;
        if (editor is not null && zone is not null)
        {
            var values = editor.GetSelectedValues(SelectedMeasurementUnit());
            ZoneLeftText.Text = FormatMeasurement(values.Left);
            ZoneTopText.Text = FormatMeasurement(values.Top);
            ZoneRightText.Text = FormatMeasurement(values.Right);
            ZoneBottomText.Text = FormatMeasurement(values.Bottom);
            ZoneWidthText.Text = FormatMeasurement(values.Width);
            ZoneHeightText.Text = FormatMeasurement(values.Height);
        }
        else
        {
            ZoneLeftText.Text = string.Empty;
            ZoneTopText.Text = string.Empty;
            ZoneRightText.Text = string.Empty;
            ZoneBottomText.Text = string.Empty;
            ZoneWidthText.Text = string.Empty;
            ZoneHeightText.Text = string.Empty;
        }

        var margins = SelectedDefinitionMode() == ZoneDefinitionMode.Margins;
        ZoneRightText.IsEnabled = margins;
        ZoneBottomText.IsEnabled = margins;
        ZoneWidthText.IsEnabled = !margins;
        ZoneHeightText.IsEnabled = !margins;
    }

    private void RefreshSafetyStatus()
    {
        var enabled = viewModel?.Settings.SnappingEnabled == true;
        SafetyStatus.Text = enabled ? "Snap-Funktion nach Speichern aktiv" : "Snap-Funktion aus";
        SafetyBadge.Background = (System.Windows.Media.Brush)FindResource(enabled ? "AccentSoftBrush" : "WarningSoftBrush");
        SafetyStatus.Foreground = (System.Windows.Media.Brush)FindResource(enabled ? "AccentStatusBrush" : "WarningBrush");
    }

    private MeasurementUnit SelectedMeasurementUnit() =>
        ZoneUnitCombo.SelectedItem is ComboBoxItem { Tag: string tag } && tag == "Pixels"
            ? MeasurementUnit.Pixels
            : MeasurementUnit.Percent;

    private ZoneDefinitionMode SelectedDefinitionMode() =>
        ZoneDefinitionCombo.SelectedItem is ComboBoxItem { Tag: string tag } && tag == "Margins"
            ? ZoneDefinitionMode.Margins
            : ZoneDefinitionMode.PositionAndSize;

    private static bool TryMeasurement(string text, double maximum, out double value)
    {
        var valid = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        return valid && double.IsFinite(value) && value >= 0 && value <= maximum;
    }

    private static string FormatMeasurement(double value) =>
        value.ToString("0.##", CultureInfo.CurrentCulture);

    private enum ZoneDefinitionMode
    {
        PositionAndSize,
        Margins
    }
}
