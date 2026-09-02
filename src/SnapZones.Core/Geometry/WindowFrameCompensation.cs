namespace SnapZones.Core.Geometry;

/// <summary>
/// Gleicht den unsichtbaren Rahmen aus, den Windows Fenstern mit veränderbarer Grösse gibt.
///
/// <para>
/// <c>GetWindowRect</c> und <c>SetWindowPos</c> rechnen mit dem Fensterrechteck, das den unsichtbaren
/// Griffbereich zum Ziehen der Fenstergrösse einschliesst. Sichtbar ist jedoch nur der vom Desktop
/// Window Manager gezeichnete Rahmen, den <c>DWMWA_EXTENDED_FRAME_BOUNDS</c> liefert. Der Unterschied
/// beträgt je nach Skalierung typischerweise sieben Pixel links, rechts und unten.
/// </para>
///
/// <para>
/// Ohne Ausgleich landen zwei Fenster in exakt aneinandergrenzenden Zonen mit einem sichtbaren Spalt
/// von der doppelten Randbreite nebeneinander, obwohl die Zonen selbst lückenlos aneinanderstossen.
/// </para>
/// </summary>
public static class WindowFrameCompensation
{
    /// <summary>
    /// Obergrenze für einen ausgleichbaren Rand. Grössere Abweichungen stammen nicht vom
    /// Griffbereich, sondern von einem Fenster, das seine Grösse selbst bestimmt; dort wird nicht
    /// ausgeglichen, damit kein Fenster unbeabsichtigt vergrössert wird.
    /// </summary>
    public const int MaximumBorderPixels = 40;

    /// <summary>
    /// Berechnet das Fensterrechteck, mit dem der sichtbare Rahmen genau auf <paramref name="target"/> zu liegen kommt.
    /// </summary>
    /// <param name="target">Die Zielzone in Bildschirmkoordinaten.</param>
    /// <param name="windowRect">Das aktuelle Fensterrechteck aus <c>GetWindowRect</c>.</param>
    /// <param name="visibleFrame">Der aktuelle sichtbare Rahmen aus <c>DWMWA_EXTENDED_FRAME_BOUNDS</c>.</param>
    /// <returns>
    /// Das ausgeglichene Rechteck, oder unverändert <paramref name="target"/>, wenn die gemessenen
    /// Ränder nicht plausibel sind.
    /// </returns>
    public static PixelRect Apply(PixelRect target, PixelRect windowRect, PixelRect visibleFrame)
    {
        if (!TryMeasure(windowRect, visibleFrame, out var left, out var top, out var right, out var bottom))
        {
            return target;
        }

        return new PixelRect(
            target.X - left,
            target.Y - top,
            target.Width + left + right,
            target.Height + top + bottom);
    }

    /// <summary>
    /// Ermittelt die unsichtbaren Ränder. Liefert <c>false</c>, wenn der sichtbare Rahmen nicht
    /// innerhalb des Fensterrechtecks liegt oder die Ränder unplausibel gross sind.
    /// </summary>
    public static bool TryMeasure(
        PixelRect windowRect,
        PixelRect visibleFrame,
        out int left,
        out int top,
        out int right,
        out int bottom)
    {
        left = visibleFrame.X - windowRect.X;
        top = visibleFrame.Y - windowRect.Y;
        right = windowRect.Right - visibleFrame.Right;
        bottom = windowRect.Bottom - visibleFrame.Bottom;

        if (windowRect.Width <= 0 || windowRect.Height <= 0 ||
            visibleFrame.Width <= 0 || visibleFrame.Height <= 0 ||
            left < 0 || top < 0 || right < 0 || bottom < 0 ||
            left > MaximumBorderPixels || top > MaximumBorderPixels ||
            right > MaximumBorderPixels || bottom > MaximumBorderPixels)
        {
            left = 0;
            top = 0;
            right = 0;
            bottom = 0;
            return false;
        }

        return left + top + right + bottom > 0;
    }
}
