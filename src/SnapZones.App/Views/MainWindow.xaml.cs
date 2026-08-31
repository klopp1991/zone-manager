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
    private MeasurementUnit zoneInputUnit = MeasurementUnit.Percent;
    private bool refreshingZoneFields;
    private bool applyingZoneFieldChange;
    private ZoneInputGroup activeZoneInputGroup = ZoneInputGroup.PositionAndSize;
    private LayoutEditorViewModel? observedEditor;

    public event Func<string, Task>? ExportConfigurationRequested;
    public event Func<string, Task>? ImportConfigurationRequested;
    public event Action? IdentifyMonitorsRequested;

    public MainWindow()
    {
        pickOverlayColor = PickOverlayColorWithDialog;
        InitializeComponent();
        InitializeShell();
    }

    public MainWindow(Func<string, string?> pickOverlayColor)
    {
        this.pickOverlayColor = pickOverlayColor ?? throw new ArgumentNullException(nameof(pickOverlayColor));
        InitializeComponent();
        InitializeShell();
    }

    private void InitializeShell()
    {
        VersionLabel.Text = $"Version {ProductInfo.Version}";
        ArrangeNavigationTabs();
    }

    private void ArrangeNavigationTabs()
    {
        NavigationTabs.Items.Clear();
        NavigationTabs.Items.Add(MonitorsTab);
        NavigationTabs.Items.Add(LayoutsTab);
        NavigationTabs.Items.Add(RulesTab);
        NavigationTabs.Items.Add(ScalingTab);
        NavigationTabs.Items.Add(SettingsTab);
        NavigationTabs.Items.Add(TransferTab);
        NavigationTabs.SelectedItem = LayoutsTab;
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
        RefreshEditor();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;
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
    }

    private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        RefreshEditor();
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
        if (!applyingZoneFieldChange &&
            !refreshingZoneFields &&
            eventArgs.PropertyName == nameof(LayoutEditorViewModel.Zones))
        {
            RefreshEditor();
        }
    }

    private async void ExportConfiguration_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        var dialog = new SaveFileDialog
        {
            Title = "Vollständige Konfiguration exportieren",
            Filter = "Sascha’s Zone Manager Vollbackup (*.swz.json)|*.swz.json|JSON-Dateien (*.json)|*.json",
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
            Filter = "Sascha’s Zone Manager Vollbackup (*.swz.json)|*.swz.json|JSON-Dateien (*.json)|*.json",
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
        _ = sender;
        _ = eventArgs;
        _ = TryApplyZoneValues(activeZoneInputGroup, true, null);
    }

    private void ZoneField_TextChanged(object sender, TextChangedEventArgs eventArgs)
    {
        _ = eventArgs;
        if (refreshingZoneFields ||
            viewModel?.Editor?.SelectedZone is null ||
            sender is not System.Windows.Controls.TextBox { Tag: string groupName } ||
            !Enum.TryParse<ZoneInputGroup>(groupName, out var group))
        {
            return;
        }

        SetActiveZoneInputGroup(group);
        _ = TryApplyZoneValues(group, false, group);
    }

    private void ZoneName_TextChanged(object sender, TextChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        var editor = viewModel?.Editor;
        if (refreshingZoneFields || editor?.SelectedZone is null)
        {
            return;
        }

        editor.RenameSelectedZone(ZoneNameText.Text);
        EditorCanvas.Zones = editor.Zones;
        ValidationText.Text = editor.ValidationMessage;
        EditorCanvas.InvalidateVisual();
    }

    private bool TryApplyZoneValues(
        ZoneInputGroup group,
        bool showErrors,
        ZoneInputGroup? preservedInputGroup)
    {
        if (viewModel?.Editor?.SelectedZone is null)
        {
            return false;
        }

        SetActiveZoneInputGroup(group);
        ClearZoneFieldErrors();
        applyingZoneFieldChange = true;
        try
        {
            if (group == ZoneInputGroup.PositionAndSize)
            {
                if (!TryReadZoneMeasurement(ZonePositionXText, true, showErrors, out var positionX) |
                    !TryReadZoneMeasurement(ZonePositionYText, false, showErrors, out var positionY) |
                    !TryReadZoneMeasurement(ZoneWidthText, true, showErrors, out var width) |
                    !TryReadZoneMeasurement(ZoneHeightText, false, showErrors, out var height))
                {
                    if (showErrors)
                    {
                        ZoneInputErrorText.Text = "Prüfe die markierten Werte. Erlaubt sind 0 bis 100 % beziehungsweise die Monitorgrösse in Pixel.";
                    }
                    return false;
                }

                var bounds = ZoneEditorGeometry.FromPositionAndSize(
                    positionX,
                    positionY,
                    width,
                    height,
                    viewModel.SelectedMonitor?.Live.WorkArea.Width ?? 1,
                    viewModel.SelectedMonitor?.Live.WorkArea.Height ?? 1);
                if (!TryValidateEditorBounds(bounds, group, showErrors))
                {
                    if (showErrors)
                    {
                        MarkZoneFieldsInvalid(ZonePositionXText, ZonePositionYText, ZoneWidthText, ZoneHeightText);
                    }
                    return false;
                }

                viewModel.Editor.UpdateSelectedZoneFromPositionAndSize(
                    ZoneNameText.Text, positionX, positionY, width, height);
            }
            else
            {
                if (!TryReadZoneMeasurement(ZoneMarginLeftText, true, showErrors, out var left) |
                    !TryReadZoneMeasurement(ZoneMarginTopText, false, showErrors, out var top) |
                    !TryReadZoneMeasurement(ZoneMarginRightText, true, showErrors, out var right) |
                    !TryReadZoneMeasurement(ZoneMarginBottomText, false, showErrors, out var bottom))
                {
                    if (showErrors)
                    {
                        ZoneInputErrorText.Text = "Prüfe die markierten Werte. Erlaubt sind 0 bis 100 % beziehungsweise die Monitorgrösse in Pixel.";
                    }
                    return false;
                }

                var bounds = ZoneEditorGeometry.FromMargins(
                    left,
                    top,
                    right,
                    bottom,
                    viewModel.SelectedMonitor?.Live.WorkArea.Width ?? 1,
                    viewModel.SelectedMonitor?.Live.WorkArea.Height ?? 1);
                if (!TryValidateEditorBounds(bounds, group, showErrors))
                {
                    if (showErrors)
                    {
                        MarkZoneFieldsInvalid(ZoneMarginLeftText, ZoneMarginTopText, ZoneMarginRightText, ZoneMarginBottomText);
                    }
                    return false;
                }

                viewModel.Editor.UpdateSelectedZoneFromMargins(
                    ZoneNameText.Text, left, top, right, bottom);
            }
        }
        finally
        {
            applyingZoneFieldChange = false;
        }

        RefreshEditor(preservedInputGroup);
        return true;
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

    private void Monitor_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs) => RefreshEditor();

    private void Layout_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs) => RefreshEditor();

    private void AddLayout_Click(object sender, RoutedEventArgs eventArgs)
    {
        viewModel?.AddLayout();
        RefreshEditor();
    }

    private void AppRuleAdd_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        viewModel?.AppRules.AddRule();
    }

    private void AppRuleDelete_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (viewModel?.AppRules.SelectedRule is not { } rule)
        {
            return;
        }

        if (System.Windows.MessageBox.Show(
                $"Die App-Regel für «{rule.DisplayName}» wird gelöscht.",
                "App-Regel löschen",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning) != MessageBoxResult.OK)
        {
            return;
        }

        viewModel.AppRules.DeleteSelectedRule();
    }

    private void AppRuleBrowse_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (viewModel is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Prozess für App-Regel auswählen",
            Filter = "Programme (*.exe)|*.exe|Alle Dateien (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
        {
            viewModel.AppRules.ProcessPath = dialog.FileName;
        }
    }

    private void LayoutName_LostFocus(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel?.SelectedLayout is null)
        {
            return;
        }

        if (string.Equals(LayoutNameText.Text, viewModel.SelectedLayout.Name, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            viewModel.RenameSelectedLayout(LayoutNameText.Text);
        }
        catch (Exception exception)
        {
            LayoutNameText.Text = viewModel.SelectedLayout.Name;
            viewModel.StatusMessage = exception.Message;
        }
    }

    private void MonitorName_LostFocus(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
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

    private void IdentifyMonitors_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        IdentifyMonitorsRequested?.Invoke();
    }

    private void MoveMonitorUp_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        viewModel?.MoveSelectedMonitorUp();
    }

    private void MoveMonitorDown_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        viewModel?.MoveSelectedMonitorDown();
    }

    private void DeleteLayout_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (viewModel?.SelectedLayout is null || viewModel.SelectedMonitor is null)
        {
            return;
        }

        var impact =
            $"Das Layout «{viewModel.SelectedLayout.Name}» für {viewModel.SelectedMonitor.UserFacingName} und seine Zonen werden gelöscht.";
        if (System.Windows.MessageBox.Show(impact, "Layout löschen", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
        {
            return;
        }

        try
        {
            viewModel.DeleteSelectedLayout();
            RefreshEditor();
        }
        catch (Exception exception)
        {
            viewModel.StatusMessage = exception.Message;
        }
    }

    private void ZoneUnit_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = eventArgs;
        if (sender is not System.Windows.Controls.Button { Tag: string unitName } ||
            !Enum.TryParse<MeasurementUnit>(unitName, out var unit) ||
            unit == zoneInputUnit)
        {
            return;
        }

        zoneInputUnit = unit;
        RefreshZoneFields();
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

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        _ = sender;
        if (eventArgs.Key is not (Key.Left or Key.Right))
        {
            return;
        }

        var sliders = new[] { OverlayOpacitySlider, ZoneGapSlider, MagnetThresholdSlider };
        var target = sliders.FirstOrDefault(slider => slider.IsMouseOver)
            ?? Keyboard.FocusedElement as Slider;
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
        ScalingResolutionText.Text = $"{monitor.WorkArea.Width} × {monitor.WorkArea.Height}";
        ScalingWorkAreaText.Text = $"{monitor.WorkArea.Width} × {monitor.WorkArea.Height} px";
        ScalingPhysicalSizeText.Text = PhysicalSizeText(monitor);
        WindowsScaleText.Text =
            $"{choice?.UserFacingName}: {scalePercent:0} % Skalierung, {monitor.DpiX:0} DPI. " +
            "Diese Werte liest das Programm nur aus; ändern lassen sie sich ausschliesslich in den Windows-Einstellungen.";
    }

    private static string PhysicalSizeText(SnapZones.Core.Monitors.LiveMonitor monitor)
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

    private void AppRuleRunningProcess_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (viewModel is null)
        {
            return;
        }

        try
        {
            var picker = new ProcessPickerWindow { Owner = this };
            if (picker.ShowDialog() == true && picker.SelectedProcessPath is { Length: > 0 } path)
            {
                viewModel.AppRules.ProcessPath = path;
            }
        }
        catch (Exception exception)
        {
            viewModel.StatusMessage = $"Die laufenden Programme konnten nicht gelesen werden: {exception.Message}";
        }
    }

    private void RefreshEditor(ZoneInputGroup? preservedInputGroup = null)
    {
        var editor = viewModel?.Editor;
        if (!LayoutNameText.IsKeyboardFocusWithin)
        {
            LayoutNameText.Text = viewModel?.SelectedLayout?.Name ?? string.Empty;
        }
        EditorCanvas.Zones = editor?.Zones ?? [];
        EditorCanvas.SelectedZoneId = editor?.SelectedZone?.Id;
        AddZoneButton.IsEnabled = editor is not null;
        DeleteZoneButton.IsEnabled = editor?.SelectedZone is not null && editor.Zones.Count > 1;
        var monitor = viewModel?.SelectedMonitor?.Live;
        if (monitor is not null)
        {
            EditorCanvas.MonitorAspectRatio = (double)monitor.WorkArea.Width / monitor.WorkArea.Height;
            EditorCanvas.MonitorPixelWidth = monitor.WorkArea.Width;
            EditorCanvas.MonitorPixelHeight = monitor.WorkArea.Height;
            EditorCanvas.MagnetThresholdPixels = viewModel?.Settings.MagnetThresholdPixels ?? 10;
        }

        RefreshScalingPage();

        RefreshZoneFields(preservedInputGroup);
        ValidationText.Text = editor?.ValidationMessage ?? string.Empty;
        EditorCanvas.InvalidateVisual();
    }

    private void RefreshZoneFields(ZoneInputGroup? preservedInputGroup = null)
    {
        refreshingZoneFields = true;
        try
        {
            var editor = viewModel?.Editor;
            var zone = editor?.SelectedZone;
            ZoneNameText.Text = zone?.Name ?? string.Empty;
            ClearZoneFieldErrors();
            if (editor is not null && zone is not null)
            {
                var percentValues = editor.GetSelectedValues(MeasurementUnit.Percent);
                var pixelValues = editor.GetSelectedValues(MeasurementUnit.Pixels);
                SetZoneFieldValue(ZoneField.PositionX, percentValues.Left, pixelValues.Left, preservedInputGroup);
                SetZoneFieldValue(ZoneField.PositionY, percentValues.Top, pixelValues.Top, preservedInputGroup);
                SetZoneFieldValue(ZoneField.Width, percentValues.Width, pixelValues.Width, preservedInputGroup);
                SetZoneFieldValue(ZoneField.Height, percentValues.Height, pixelValues.Height, preservedInputGroup);
                SetZoneFieldValue(ZoneField.MarginLeft, percentValues.Left, pixelValues.Left, preservedInputGroup);
                SetZoneFieldValue(ZoneField.MarginTop, percentValues.Top, pixelValues.Top, preservedInputGroup);
                SetZoneFieldValue(ZoneField.MarginRight, percentValues.Right, pixelValues.Right, preservedInputGroup);
                SetZoneFieldValue(ZoneField.MarginBottom, percentValues.Bottom, pixelValues.Bottom, preservedInputGroup);
            }
            else
            {
                foreach (var field in Enum.GetValues<ZoneField>())
                {
                    if (preservedInputGroup != InputGroupFor(field))
                    {
                        TextBoxFor(field).Text = string.Empty;
                    }
                }
            }

            UpdateUnitSegments();
            UpdateZoneInputGroupPresentation();
        }
        finally
        {
            refreshingZoneFields = false;
        }
    }

    private void SetZoneFieldValue(
        ZoneField field,
        double percentValue,
        double pixelValue,
        ZoneInputGroup? preservedInputGroup)
    {
        if (preservedInputGroup != InputGroupFor(field))
        {
            TextBoxFor(field).Text = FormatMeasurement(
                zoneInputUnit == MeasurementUnit.Percent ? percentValue : pixelValue);
        }
    }

    private bool TryReadZoneMeasurement(
        System.Windows.Controls.TextBox textBox,
        bool horizontal,
        bool showErrors,
        out ZoneMeasurement measurement)
    {
        var maximum = zoneInputUnit == MeasurementUnit.Percent
            ? 100d
            : horizontal
                ? viewModel?.SelectedMonitor?.Live.WorkArea.Width ?? 1
                : viewModel?.SelectedMonitor?.Live.WorkArea.Height ?? 1;
        if (TryMeasurement(textBox.Text, maximum, out var value))
        {
            measurement = new ZoneMeasurement(value, zoneInputUnit);
            return true;
        }

        if (showErrors)
        {
            textBox.SetResourceReference(Border.BorderBrushProperty, "DangerBrush");
        }
        measurement = default;
        return false;
    }

    private bool TryValidateEditorBounds(
        NormalizedRect bounds,
        ZoneInputGroup group,
        bool showErrors)
    {
        const double tolerance = 0.000001;
        var valid = bounds.X >= -tolerance &&
                    bounds.Y >= -tolerance &&
                    bounds.Width > tolerance &&
                    bounds.Height > tolerance &&
                    bounds.X + bounds.Width <= 1 + tolerance &&
                    bounds.Y + bounds.Height <= 1 + tolerance;
        if (!valid && showErrors)
        {
            ZoneInputErrorText.Text = group == ZoneInputGroup.PositionAndSize
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
    }

    private void UpdateUnitSegments()
    {
        var pixels = zoneInputUnit == MeasurementUnit.Pixels;
        ZoneUnitPercentButton.Style = (Style)FindResource(pixels ? "UnitSegment" : "UnitSegmentActive");
        ZoneUnitPixelButton.Style = (Style)FindResource(pixels ? "UnitSegmentActive" : "UnitSegment");
        AutomationProperties.SetName(
            ZoneUnitPercentButton,
            pixels ? "Einheit Prozent für alle acht Werte" : "Einheit Prozent für alle acht Werte, aktiv");
        AutomationProperties.SetName(
            ZoneUnitPixelButton,
            pixels ? "Einheit Pixel für alle acht Werte, aktiv" : "Einheit Pixel für alle acht Werte");
        var unitLabel = pixels ? "Pixel" : "Prozent";
        var suffix = pixels ? "px" : "%";
        foreach (var field in Enum.GetValues<ZoneField>())
        {
            var textBox = TextBoxFor(field);
            AutomationProperties.SetName(textBox, $"{FieldLabel(field)} in {unitLabel}");
            textBox.ToolTip = $"Aktuelle Einheit: {suffix}. Die Umschaltung oben in der Karte gilt für alle acht Werte.";
        }
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
