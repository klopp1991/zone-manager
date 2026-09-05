using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using SnapZones.App.ViewModels;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.App.Controls;

/// <summary>
/// Das Werte-Panel des Layout-Editors: Name, die acht Masse in einer Einheit und die Auffangzone. Es wird
/// im Fenster rechts neben der Zeichenflaeche und im Vollbild-Editor als Popover verwendet und bezieht
/// seinen Zustand aus dem <see cref="LayoutEditorViewModel"/> des Fensters.
/// </summary>
public partial class ZoneValuesPanel : System.Windows.Controls.UserControl
{
    private LayoutEditorViewModel? editor;
    private MeasurementUnit zoneInputUnit = MeasurementUnit.Percent;
    private bool refreshingZoneFields;
    private bool applyingZoneFieldChange;
    private ZoneInputGroup activeZoneInputGroup = ZoneInputGroup.PositionAndSize;
    private int monitorWidth = 1;
    private int monitorHeight = 1;

    public ZoneValuesPanel()
    {
        InitializeComponent();
        UpdateUnitSegments();
    }

    /// <summary>Der Aufrufer hat die Zone geaendert; Zeichenflaeche und Panel muessen nachziehen.</summary>
    public event EventHandler? ValuesApplied;

    /// <summary>Die aktuelle Einheit aller acht Felder.</summary>
    public MeasurementUnit Unit => zoneInputUnit;

    /// <summary>Verbindet das Panel mit dem Editor und der Monitorgroesse und fuellt die Felder.</summary>
    public void Attach(LayoutEditorViewModel? layoutEditor, int pixelWidth, int pixelHeight)
    {
        editor = layoutEditor;
        monitorWidth = Math.Max(1, pixelWidth);
        monitorHeight = Math.Max(1, pixelHeight);
        Refresh();
    }

    /// <summary>Liest den Zustand des Editors neu in die Felder, ohne die gerade bearbeitete Gruppe zu ueberschreiben.</summary>
    public void Refresh(ZoneInputGroup? preservedInputGroup = null)
    {
        refreshingZoneFields = true;
        try
        {
            var zone = editor?.SelectedZone;
            ZoneTitleText.Text = zone is null ? "Keine Zone ausgewählt" : $"Zone {ZoneNumber(zone)}";
            SetZoneNameText(zone?.Name ?? string.Empty);
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
                var shown = zoneInputUnit == MeasurementUnit.Percent ? percentValues : pixelValues;
                MarginsSummaryText.Text = string.Join(" · ", new[] { shown.Left, shown.Top, shown.Right, shown.Bottom }.Select(FormatMeasurement));
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

                MarginsSummaryText.Text = string.Empty;
            }

            MainZoneCheckBox.IsEnabled = zone is not null;
            MainZoneCheckBox.IsChecked = editor?.IsSelectedZoneMainZone ?? false;
            MainZoneStateText.Text = editor?.MainZoneStateText ?? string.Empty;
            ValidationText.Text = editor?.ValidationMessage ?? string.Empty;
            IsEnabled = editor is not null;
            UpdateUnitSegments();
            UpdateZoneInputGroupPresentation();
        }
        finally
        {
            refreshingZoneFields = false;
        }
    }

    private int ZoneNumber(ZoneDefinition zone)
    {
        var zones = editor?.Zones ?? [];
        for (var index = 0; index < zones.Count; index++)
        {
            if (zones[index].Id == zone.Id)
            {
                return index + 1;
            }
        }

        return 0;
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
        Refresh();
    }

    private void MarginsExpander_Expanded(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        SetActiveZoneInputGroup(ZoneInputGroup.Margins);
    }

    private void MainZone_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (editor?.SelectedZone is null)
        {
            return;
        }

        editor.ToggleSelectedZoneAsMainZone();
        Refresh();
        ValuesApplied?.Invoke(this, EventArgs.Empty);
    }

    private void ZoneField_TextChanged(object sender, TextChangedEventArgs eventArgs)
    {
        _ = eventArgs;
        if (refreshingZoneFields ||
            editor?.SelectedZone is null ||
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
        if (refreshingZoneFields || editor?.SelectedZone is null)
        {
            return;
        }

        applyingZoneFieldChange = true;
        try
        {
            editor.RenameSelectedZone(ZoneNameText.Text);
        }
        finally
        {
            applyingZoneFieldChange = false;
        }

        MainZoneStateText.Text = editor.MainZoneStateText;
        ValidationText.Text = editor.ValidationMessage;
        ValuesApplied?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Wahr, solange das Panel selbst gerade eine Aenderung in den Editor schreibt.</summary>
    public bool IsApplyingChange => applyingZoneFieldChange || refreshingZoneFields;

    private bool TryApplyZoneValues(
        ZoneInputGroup group,
        bool showErrors,
        ZoneInputGroup? preservedInputGroup)
    {
        if (editor?.SelectedZone is null)
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

                var bounds = ZoneEditorGeometry.FromPositionAndSize(positionX, positionY, width, height, monitorWidth, monitorHeight);
                if (!TryValidateEditorBounds(bounds, group, showErrors))
                {
                    if (showErrors)
                    {
                        MarkZoneFieldsInvalid(ZonePositionXText, ZonePositionYText, ZoneWidthText, ZoneHeightText);
                    }
                    return false;
                }

                editor.UpdateSelectedZoneFromPositionAndSize(ZoneNameText.Text, positionX, positionY, width, height);
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

                var bounds = ZoneEditorGeometry.FromMargins(left, top, right, bottom, monitorWidth, monitorHeight);
                if (!TryValidateEditorBounds(bounds, group, showErrors))
                {
                    if (showErrors)
                    {
                        MarkZoneFieldsInvalid(ZoneMarginLeftText, ZoneMarginTopText, ZoneMarginRightText, ZoneMarginBottomText);
                    }
                    return false;
                }

                editor.UpdateSelectedZoneFromMargins(ZoneNameText.Text, left, top, right, bottom);
            }
        }
        finally
        {
            applyingZoneFieldChange = false;
        }

        Refresh(preservedInputGroup);
        ValuesApplied?.Invoke(this, EventArgs.Empty);
        return true;
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
            _ = TryApplyZoneValues(activeZoneInputGroup, true, null);
            eventArgs.Handled = true;
        }
    }

    private void SetZoneNameText(string name)
    {
        if (string.Equals(ZoneNameText.Text, name, StringComparison.Ordinal))
        {
            return;
        }

        // Während der Eingabe darf der getrimmte Name die Rohfassung im Feld nicht ersetzen,
        // sonst springt der Cursor bei jedem Leerschlag an den Anfang.
        if (ZoneNameText.IsKeyboardFocusWithin &&
            string.Equals(ZoneNameText.Text.Trim(), name, StringComparison.Ordinal))
        {
            return;
        }

        var caretIndex = ZoneNameText.CaretIndex;
        ZoneNameText.Text = name;
        ZoneNameText.CaretIndex = Math.Min(caretIndex, name.Length);
    }

    private void SetZoneFieldValue(ZoneField field, double percentValue, double pixelValue, ZoneInputGroup? preservedInputGroup)
    {
        if (preservedInputGroup != InputGroupFor(field))
        {
            TextBoxFor(field).Text = FormatMeasurement(zoneInputUnit == MeasurementUnit.Percent ? percentValue : pixelValue);
        }
    }

    private bool TryReadZoneMeasurement(System.Windows.Controls.TextBox textBox, bool horizontal, bool showErrors, out ZoneMeasurement measurement)
    {
        var maximum = zoneInputUnit == MeasurementUnit.Percent
            ? 100d
            : horizontal ? monitorWidth : monitorHeight;
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

    private bool TryValidateEditorBounds(NormalizedRect bounds, ZoneInputGroup group, bool showErrors)
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

    private static void MarkZoneFieldsInvalid(params System.Windows.Controls.TextBox[] fields)
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
            textBox.ToolTip = $"Aktuelle Einheit: {suffix}. Die Umschaltung oben gilt für alle acht Werte.";
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

    public enum ZoneInputGroup
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
}
