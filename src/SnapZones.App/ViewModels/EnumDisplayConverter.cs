using System.Globalization;
using System.Windows.Data;
using SnapZones.Core.AppRules;
using SnapZones.Core.Models;

namespace SnapZones.App.ViewModels;

public sealed class EnumDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;
        return value switch
        {
            OverlayScope.AllMonitors => "Alle Monitore",
            OverlayScope.ActiveMonitor => "Monitor beim Ziehbeginn",
            OverlayScope.CursorMonitor => "Monitor unter dem Mauszeiger",
            TriggerMode.Immediate => "Sofort beim Ziehen",
            TriggerMode.ShiftKey => "Nur mit Umschalttaste",
            ThemeMode.System => "Windows-System",
            ThemeMode.Light => "Hell",
            ThemeMode.Dark => "Dunkel",
            ElevationMode.WhenNeeded => "Nur wenn nötig (empfohlen)",
            ElevationMode.Always => "Immer beim Start",
            FixedSizeWindowPlacement.Center => "In der Zone zentrieren",
            FixedSizeWindowPlacement.TopLeft => "Oben links in der Zone",
            FixedSizeWindowPlacement.Leave => "Nicht anfassen",
            ZoneHotkeyModifiers.ControlAlt => "Ctrl + Alt",
            ZoneHotkeyModifiers.ControlShift => "Ctrl + Shift",
            ZoneHotkeyModifiers.AltShift => "Alt + Shift",
            ZoneHotkeyModifiers.ControlWin => "Ctrl + Win",
            OverlayLabelStyle.NumberAndName => "Nummer und Name",
            OverlayLabelStyle.NumberOnly => "Nur Nummer",
            OverlayLabelStyle.NameOnly => "Nur Name",
            AppRuleEvent.WindowCreated => "Fenster wird geöffnet",
            AppRuleEvent.WindowFocused => "Fenster erhält den Fokus",
            AppRuleEvent.LayoutActivated => "Layout wird aktiviert",
            _ => value.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
