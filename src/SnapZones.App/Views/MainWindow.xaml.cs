using System.Globalization;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using SnapZones.App.Controls;
using SnapZones.App.ViewModels;
using SnapZones.Core.Editor;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.App.Views;

public partial class MainWindow : Window
{
    private MainViewModel? viewModel;
    private readonly Func<string, string?> pickOverlayColor;
    private readonly Dictionary<ZoneField, MeasurementUnit> zoneFieldUnits = Enum
        .GetValues<ZoneField>()
        .ToDictionary(field => field, _ => MeasurementUnit.Percent);
    private ZoneInputGroup activeZoneInputGroup = ZoneInputGroup.PositionAndSize;

    public event Func<string, Task>? ExportConfigurationRequested;
    public event Func<string, Task>? ImportConfigurationRequested;

    public MainWindow()
    {
        pickOverlayColor = PickOverlayColorWithDialog;
        InitializeComponent();
    }

    public MainWindow(Func<string, string?> pickOverlayColor)
    {
        this.pickOverlayColor = pickOverlayColor ?? throw new ArgumentNullException(nameof(pickOverlayColor));
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

    private async void ExportConfiguration_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        var dialog = new SaveFileDialog
        {
            Title = "Vollständige Konfiguration exportieren",
            Filter = "Sascha Window Zones Vollbackup (*.swz.json)|*.swz.json|JSON-Dateien (*.json)|*.json",
            DefaultExt = ".swz.json",
            AddExtension = true,
            FileName = $"SaschaWindowZones-Vollbackup-{DateTime.Now:yyyy-MM-dd-HHmm}.swz.json"
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
            Filter = "Sascha Window Zones Vollbackup (*.swz.json)|*.swz.json|JSON-Dateien (*.json)|*.json",
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

    private void Template_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel?.Editor is null ||
            sender is not System.Windows.Controls.Button { DataContext: LayoutSuggestion suggestion })
        {
            return;
        }

        viewModel.Editor.ApplyTemplate(suggestion.Template);
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

        ClearZoneFieldErrors();
        if (activeZoneInputGroup == ZoneInputGroup.PositionAndSize)
        {
            if (!TryReadZoneMeasurement(ZonePositionXText, ZoneField.PositionX, true, out var positionX) |
                !TryReadZoneMeasurement(ZonePositionYText, ZoneField.PositionY, false, out var positionY) |
                !TryReadZoneMeasurement(ZoneWidthText, ZoneField.Width, true, out var width) |
                !TryReadZoneMeasurement(ZoneHeightText, ZoneField.Height, false, out var height))
            {
                ZoneInputErrorText.Text = "Prüfe die markierten Werte. Erlaubt sind 0 bis 100 % beziehungsweise die Monitorgrösse in Pixel.";
                return;
            }

            var bounds = ZoneEditorGeometry.FromPositionAndSize(
                positionX,
                positionY,
                width,
                height,
                viewModel.SelectedMonitor?.Live.WorkArea.Width ?? 1,
                viewModel.SelectedMonitor?.Live.WorkArea.Height ?? 1);
            if (!TryValidateEditorBounds(bounds))
            {
                MarkZoneFieldsInvalid(ZonePositionXText, ZonePositionYText, ZoneWidthText, ZoneHeightText);
                return;
            }

            viewModel.Editor.UpdateSelectedZoneFromPositionAndSize(
                ZoneNameText.Text, positionX, positionY, width, height);
        }
        else
        {
            if (!TryReadZoneMeasurement(ZoneMarginLeftText, ZoneField.MarginLeft, true, out var left) |
                !TryReadZoneMeasurement(ZoneMarginTopText, ZoneField.MarginTop, false, out var top) |
                !TryReadZoneMeasurement(ZoneMarginRightText, ZoneField.MarginRight, true, out var right) |
                !TryReadZoneMeasurement(ZoneMarginBottomText, ZoneField.MarginBottom, false, out var bottom))
            {
                ZoneInputErrorText.Text = "Prüfe die markierten Werte. Erlaubt sind 0 bis 100 % beziehungsweise die Monitorgrösse in Pixel.";
                return;
            }

            var bounds = ZoneEditorGeometry.FromMargins(
                left,
                top,
                right,
                bottom,
                viewModel.SelectedMonitor?.Live.WorkArea.Width ?? 1,
                viewModel.SelectedMonitor?.Live.WorkArea.Height ?? 1);
            if (!TryValidateEditorBounds(bounds))
            {
                MarkZoneFieldsInvalid(ZoneMarginLeftText, ZoneMarginTopText, ZoneMarginRightText, ZoneMarginBottomText);
                return;
            }

            viewModel.Editor.UpdateSelectedZoneFromMargins(
                ZoneNameText.Text, left, top, right, bottom);
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

    private void PlacementSelect_Click(object sender, RoutedEventArgs eventArgs) =>
        RunPlacementAction(() => viewModel?.WindowPlacement.RequestWindowSelection());

    private void PlacementApply_Click(object sender, RoutedEventArgs eventArgs) =>
        RunPlacementAction(() => viewModel?.WindowPlacement.ApplySelectedNow());

    private void PlacementRemember_Click(object sender, RoutedEventArgs eventArgs) =>
        RunPlacementAction(() => viewModel?.WindowPlacement.RememberSelected());

    private void PlacementExclude_Click(object sender, RoutedEventArgs eventArgs) =>
        RunPlacementAction(() => viewModel?.WindowPlacement.ExcludeSelected());

    private void PlacementForget_Click(object sender, RoutedEventArgs eventArgs) =>
        RunPlacementAction(() => viewModel?.WindowPlacement.ForgetSelected());

    private void PlacementFix_Click(object sender, RoutedEventArgs eventArgs) =>
        RunPlacementAction(() => viewModel?.WindowPlacement.FixSelectedToZone());

    private void RunPlacementAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            if (viewModel is not null)
            {
                viewModel.StatusMessage = exception.Message;
            }
        }
    }

    private void ProfileName_LostFocus(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel is null)
        {
            return;
        }

        if (string.Equals(ProfileNameText.Text, viewModel.SelectedProfile.Name, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            viewModel.RenameSelectedProfile(ProfileNameText.Text);
        }
        catch (Exception exception)
        {
            ProfileNameText.Text = viewModel.SelectedProfile.Name;
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

    private void ZoneUnitButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = eventArgs;
        if (sender is not System.Windows.Controls.Button { Tag: string fieldName } button ||
            !Enum.TryParse<ZoneField>(fieldName, out var field))
        {
            return;
        }

        SetActiveZoneInputGroup(InputGroupFor(field));
        var oldUnit = zoneFieldUnits[field];
        var newUnit = oldUnit == MeasurementUnit.Percent ? MeasurementUnit.Pixels : MeasurementUnit.Percent;
        var textBox = TextBoxFor(field);
        var axisPixels = AxisPixelsFor(field);
        if (double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var currentValue) &&
            double.IsFinite(currentValue))
        {
            var convertedValue = oldUnit == MeasurementUnit.Percent
                ? currentValue / 100d * axisPixels
                : currentValue / axisPixels * 100d;
            textBox.Text = FormatMeasurement(convertedValue);
        }

        zoneFieldUnits[field] = newUnit;
        UpdateUnitButton(button, field);
    }

    private void ZoneField_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs eventArgs)
    {
        _ = eventArgs;
        if (sender is System.Windows.Controls.TextBox { Tag: string groupName } &&
            Enum.TryParse<ZoneInputGroup>(groupName, out var group))
        {
            SetActiveZoneInputGroup(group);
        }
    }

    private void ZoneField_KeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (sender is System.Windows.Controls.TextBox { Tag: string groupName } &&
            Enum.TryParse<ZoneInputGroup>(groupName, out var group))
        {
            SetActiveZoneInputGroup(group);
        }

        if (eventArgs.Key == Key.Enter)
        {
            ApplyZoneValues_Click(sender, eventArgs);
            eventArgs.Handled = true;
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
        ClearZoneFieldErrors();
        if (editor is not null && zone is not null)
        {
            var percentValues = editor.GetSelectedValues(MeasurementUnit.Percent);
            var pixelValues = editor.GetSelectedValues(MeasurementUnit.Pixels);
            SetZoneFieldValue(ZoneField.PositionX, percentValues.Left, pixelValues.Left);
            SetZoneFieldValue(ZoneField.PositionY, percentValues.Top, pixelValues.Top);
            SetZoneFieldValue(ZoneField.Width, percentValues.Width, pixelValues.Width);
            SetZoneFieldValue(ZoneField.Height, percentValues.Height, pixelValues.Height);
            SetZoneFieldValue(ZoneField.MarginLeft, percentValues.Left, pixelValues.Left);
            SetZoneFieldValue(ZoneField.MarginTop, percentValues.Top, pixelValues.Top);
            SetZoneFieldValue(ZoneField.MarginRight, percentValues.Right, pixelValues.Right);
            SetZoneFieldValue(ZoneField.MarginBottom, percentValues.Bottom, pixelValues.Bottom);
        }
        else
        {
            foreach (var field in Enum.GetValues<ZoneField>())
            {
                TextBoxFor(field).Text = string.Empty;
                UpdateUnitButton(ButtonFor(field), field);
            }
        }

        UpdateZoneInputGroupPresentation();
    }

    private void RefreshSafetyStatus()
    {
        var enabled = viewModel?.Settings.SnappingEnabled == true;
        SafetyStatus.Text = enabled ? "Snap-Funktion aktiv" : "Snap-Funktion aus";
        SafetyBadge.Background = (System.Windows.Media.Brush)FindResource(enabled ? "AccentSoftBrush" : "WarningSoftBrush");
        SafetyStatus.Foreground = (System.Windows.Media.Brush)FindResource(enabled ? "AccentStatusBrush" : "WarningBrush");
    }

    private void SetZoneFieldValue(ZoneField field, double percentValue, double pixelValue)
    {
        TextBoxFor(field).Text = FormatMeasurement(
            zoneFieldUnits[field] == MeasurementUnit.Percent ? percentValue : pixelValue);
        UpdateUnitButton(ButtonFor(field), field);
    }

    private bool TryReadZoneMeasurement(
        System.Windows.Controls.TextBox textBox,
        ZoneField field,
        bool horizontal,
        out ZoneMeasurement measurement)
    {
        var unit = zoneFieldUnits[field];
        var maximum = unit == MeasurementUnit.Percent
            ? 100d
            : horizontal
                ? viewModel?.SelectedMonitor?.Live.WorkArea.Width ?? 1
                : viewModel?.SelectedMonitor?.Live.WorkArea.Height ?? 1;
        if (TryMeasurement(textBox.Text, maximum, out var value))
        {
            measurement = new ZoneMeasurement(value, unit);
            return true;
        }

        textBox.SetResourceReference(Border.BorderBrushProperty, "DangerBrush");
        measurement = default;
        return false;
    }

    private bool TryValidateEditorBounds(NormalizedRect bounds)
    {
        const double tolerance = 0.000001;
        var valid = bounds.X >= -tolerance &&
                    bounds.Y >= -tolerance &&
                    bounds.Width > tolerance &&
                    bounds.Height > tolerance &&
                    bounds.X + bounds.Width <= 1 + tolerance &&
                    bounds.Y + bounds.Height <= 1 + tolerance;
        if (!valid)
        {
            ZoneInputErrorText.Text = activeZoneInputGroup == ZoneInputGroup.PositionAndSize
                ? "Position und Grösse müssen vollständig innerhalb der Monitorfläche liegen."
                : "Die gegenüberliegenden Abstände müssen Platz für eine Zone übriglassen.";
        }

        return valid;
    }

    private void MarkZoneFieldsInvalid(params System.Windows.Controls.TextBox[] fields)
    {
        foreach (var field in fields)
        {
            field.SetResourceReference(Border.BorderBrushProperty, "DangerBrush");
        }
    }

    private void ClearZoneFieldErrors()
    {
        ZoneInputErrorText.Text = string.Empty;
        foreach (var field in Enum.GetValues<ZoneField>())
        {
            TextBoxFor(field).SetResourceReference(Border.BorderBrushProperty, "ControlBorderBrush");
        }
    }

    private void SetActiveZoneInputGroup(ZoneInputGroup group)
    {
        activeZoneInputGroup = group;
        UpdateZoneInputGroupPresentation();
    }

    private void UpdateZoneInputGroupPresentation()
    {
        ZonePositionGroupBorder.SetResourceReference(
            Border.BorderBrushProperty,
            activeZoneInputGroup == ZoneInputGroup.PositionAndSize ? "AccentBrush" : "BorderBrush");
        ZoneMarginsGroupBorder.SetResourceReference(
            Border.BorderBrushProperty,
            activeZoneInputGroup == ZoneInputGroup.Margins ? "AccentBrush" : "BorderBrush");
        ApplyZoneValuesButton.Content = activeZoneInputGroup == ZoneInputGroup.PositionAndSize
            ? "Position und Grösse anwenden"
            : "Abstände anwenden";
    }

    private void UpdateUnitButton(System.Windows.Controls.Button button, ZoneField field)
    {
        var pixels = zoneFieldUnits[field] == MeasurementUnit.Pixels;
        button.Content = pixels ? "px" : "%";
        var unitName = pixels ? "Pixel" : "Prozent";
        AutomationProperties.SetName(button, $"Einheit für {FieldLabel(field)}: {unitName}");
        button.ToolTip = $"Aktuell {unitName}. Klicken zum Umschalten.";
    }

    private int AxisPixelsFor(ZoneField field)
    {
        var workArea = viewModel?.SelectedMonitor?.Live.WorkArea;
        return field is ZoneField.PositionX or ZoneField.Width or ZoneField.MarginLeft or ZoneField.MarginRight
            ? workArea?.Width ?? 1
            : workArea?.Height ?? 1;
    }

    private System.Windows.Controls.TextBox TextBoxFor(ZoneField field) => field switch
    {
        ZoneField.PositionX => ZonePositionXText,
        ZoneField.PositionY => ZonePositionYText,
        ZoneField.Width => ZoneWidthText,
        ZoneField.Height => ZoneHeightText,
        ZoneField.MarginLeft => ZoneMarginLeftText,
        ZoneField.MarginTop => ZoneMarginTopText,
        ZoneField.MarginRight => ZoneMarginRightText,
        ZoneField.MarginBottom => ZoneMarginBottomText,
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };

    private System.Windows.Controls.Button ButtonFor(ZoneField field) => field switch
    {
        ZoneField.PositionX => ZonePositionXUnitButton,
        ZoneField.PositionY => ZonePositionYUnitButton,
        ZoneField.Width => ZoneWidthUnitButton,
        ZoneField.Height => ZoneHeightUnitButton,
        ZoneField.MarginLeft => ZoneMarginLeftUnitButton,
        ZoneField.MarginTop => ZoneMarginTopUnitButton,
        ZoneField.MarginRight => ZoneMarginRightUnitButton,
        ZoneField.MarginBottom => ZoneMarginBottomUnitButton,
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };

    private static ZoneInputGroup InputGroupFor(ZoneField field) => field is
        ZoneField.PositionX or ZoneField.PositionY or ZoneField.Width or ZoneField.Height
            ? ZoneInputGroup.PositionAndSize
            : ZoneInputGroup.Margins;

    private static string FieldLabel(ZoneField field) => field switch
    {
        ZoneField.PositionX => "X-Position",
        ZoneField.PositionY => "Y-Position",
        ZoneField.Width => "Breite",
        ZoneField.Height => "Höhe",
        ZoneField.MarginLeft => "Abstand links",
        ZoneField.MarginTop => "Abstand oben",
        ZoneField.MarginRight => "Abstand rechts",
        ZoneField.MarginBottom => "Abstand unten",
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };

    private static bool TryMeasurement(string text, double maximum, out double value)
    {
        var valid = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        return valid && double.IsFinite(value) && value >= 0 && value <= maximum;
    }

    private static string FormatMeasurement(double value) =>
        value.ToString("0.##", CultureInfo.CurrentCulture);

    private enum ZoneInputGroup
    {
        PositionAndSize,
        Margins
    }

    private enum ZoneField
    {
        PositionX,
        PositionY,
        Width,
        Height,
        MarginLeft,
        MarginTop,
        MarginRight,
        MarginBottom
    }

    private sealed class DialogOwner(nint handle) : System.Windows.Forms.IWin32Window
    {
        public nint Handle { get; } = handle;
    }
}
