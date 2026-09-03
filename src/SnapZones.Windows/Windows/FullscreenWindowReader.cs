using System.Runtime.InteropServices;
using SnapZones.Core.Geometry;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Windows;

/// <param name="Bounds">Das Fensterrechteck aus <c>GetWindowRect</c>.</param>
/// <param name="MonitorBounds">Die ganze Flaeche des Monitors, auf dem das Fenster liegt.</param>
/// <param name="IsBorderless">
/// Ob das Fenster weder Titelleiste noch Griffrahmen hat. Ein Browser legt beide im Vollbild ab; solange
/// sie fehlen, versteht sich das Programm noch als Vollbild, auch wenn sein Rechteck gerade nicht den
/// ganzen Monitor deckt.
/// </param>
public readonly record struct FullscreenWindowState(
    PixelRect Bounds,
    PixelRect MonitorBounds,
    bool IsMaximized,
    bool IsMinimized,
    bool IsBorderless = false);

/// <summary>
/// Die schmale Fensterabfrage fuer das Zonen-Vollbild: Rechteck, Monitorflaeche und Zustand.
///
/// <para>
/// Bewusst getrennt von <see cref="IPlacementWindowService.Inspect"/>. Jenes liest zusaetzlich
/// Programmpfad, App-Kennung und Fenstertitel, um ein Fenster ueber Sitzungen hinweg wiederzuerkennen —
/// noetig fuer den Katalog, aber zu teuer fuer eine Abfrage, die bei jedem Fensterereignis laeuft.
/// Waehrend eines Ziehvorgangs meldet Windows Dutzende Ereignisse je Sekunde.
/// </para>
/// </summary>
public interface IFullscreenWindowReader
{
    /// <summary>Der Zustand des Fensters, oder <c>null</c>, wenn es nicht mehr lesbar ist.</summary>
    FullscreenWindowState? Read(nint window);
}

public sealed class WindowsFullscreenWindowReader : IFullscreenWindowReader
{
    private const uint DefaultToNearestMonitor = 2;

    public FullscreenWindowState? Read(nint window)
    {
        if (window == 0 || !User32.IsWindow(window) || !User32.IsWindowVisible(window))
        {
            return null;
        }

        if (!User32.GetWindowRect(window, out var rectangle))
        {
            return null;
        }

        var monitor = User32.MonitorFromWindow(window, DefaultToNearestMonitor);
        if (monitor == 0)
        {
            return null;
        }

        var monitorInfo = new MonitorInfoEx
        {
            Size = (uint)Marshal.SizeOf<MonitorInfoEx>(),
            DeviceName = string.Empty
        };
        if (!User32.GetMonitorInfo(monitor, ref monitorInfo))
        {
            return null;
        }

        var style = User32.GetWindowLongPtr(window, WindowEligibility.StyleIndex).ToInt64();
        var borderless = (style & WindowEligibility.CaptionStyle) != WindowEligibility.CaptionStyle &&
            (style & WindowEligibility.ThickFrameStyle) == 0;

        return new FullscreenWindowState(
            WindowEligibility.ToPixelRect(rectangle),
            WindowEligibility.ToPixelRect(monitorInfo.Monitor),
            User32.IsZoomed(window),
            User32.IsIconic(window),
            borderless);
    }
}
