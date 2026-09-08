namespace SnapZones.Core.Geometry;

/// <summary>
/// Ob ein Fenster die ganze Monitorflaeche einnimmt.
///
/// <para>
/// Gebraucht vom Positionsgedaechtnis: ein rahmenloses Fenster ueber dem ganzen Monitor ist ein
/// Vollbild, und dessen Rechteck ist kein Ort, den jemand gewaehlt hat. Wuerde es gemerkt, erschiene
/// ein im Vollbild geschlossener Browser beim naechsten Start monitorfuellend statt in seiner Zone.
/// </para>
/// </summary>
public static class MonitorCoverage
{
    /// <summary>
    /// Wie weit das Fensterrechteck von den Monitorkanten abweichen darf und trotzdem als deckend gilt.
    /// Manche Programme setzen sich um ein Pixel groesser als der Monitor.
    /// </summary>
    public const int TolerancePixels = 2;

    /// <summary>
    /// Gemeinsame Vollbildregel fuer Positionsgedaechtnis und Platzierung; maximierte Fenster bleiben
    /// auch bei automatisch ausgeblendeter Taskleiste vom Vollbild unterschieden.
    /// </summary>
    public static bool IsFullscreen(
        PixelRect windowBounds, PixelRect monitorBounds,
        bool hasCaption, bool isMinimized, bool isMaximized) =>
        !hasCaption && !isMinimized && !isMaximized && Covers(windowBounds, monitorBounds);

    /// <summary>
    /// Ob das Fenster die volle Monitorflaeche einnimmt. Gemessen wird gegen die ganze Monitorflaeche,
    /// nicht gegen die Arbeitsflaeche: ein maximiertes Fenster endet an der Taskleiste und faellt damit
    /// von selbst heraus. Nur wenn die Taskleiste automatisch ausgeblendet wird, decken sich beide
    /// Flaechen — deshalb prueft der Aufrufer zusaetzlich, dass das Fenster nicht maximiert ist.
    /// </summary>
    public static bool Covers(PixelRect windowBounds, PixelRect monitorBounds) =>
        Covers(windowBounds, monitorBounds, TolerancePixels);

    public static bool Covers(PixelRect windowBounds, PixelRect monitorBounds, int tolerancePixels)
    {
        if (windowBounds.Width <= 0 || windowBounds.Height <= 0 ||
            monitorBounds.Width <= 0 || monitorBounds.Height <= 0)
        {
            return false;
        }

        var tolerance = Math.Max(0, tolerancePixels);
        return windowBounds.X <= monitorBounds.X + tolerance &&
            windowBounds.Y <= monitorBounds.Y + tolerance &&
            windowBounds.Right >= monitorBounds.Right - tolerance &&
            windowBounds.Bottom >= monitorBounds.Bottom - tolerance;
    }
}
