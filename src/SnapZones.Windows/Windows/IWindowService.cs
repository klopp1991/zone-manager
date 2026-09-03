using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;
using SnapZones.Core.Layouts;
using SnapZones.Core.PartMonitors;

namespace SnapZones.Windows.Windows;

public interface IWindowService : IPartMonitorWindowGateway
{
    WindowSnapshot? Inspect(nint window, PointInt cursor, int ownProcessId);
    bool TrySnap(nint window, PixelRect bounds);
    IReadOnlyList<WindowPlacement> GetMovableTopLevelWindows(int ownProcessId);
    WindowRuleCandidate? InspectRuleCandidate(nint window, int ownProcessId);
    IReadOnlyList<WindowRuleCandidate> GetRuleCandidates(int ownProcessId);
    bool TryGetCursorPosition(out PointInt point);
    bool IsEscapePressed();
    bool IsShiftPressed();

    /// <summary>Ob Strg gedrueckt ist. Waehlt beim Ziehen mehrere Zonen gleichzeitig aus.</summary>
    bool IsControlPressed();

    /// <summary>
    /// Ob das Fenster nur mit Administratorrechten bewegt werden koennte, weil es einem hoeher
    /// berechtigten Programm gehoert.
    /// </summary>
    bool RequiresElevation(nint window);

    /// <summary>
    /// Wie <see cref="TrySnap"/>, aber mit gemessenem Ergebnis und Begruendung. Die Vorgabe reicht das
    /// Ja/Nein durch, damit Fakes in Tests ohne Aenderung weiterlaufen.
    /// </summary>
    PlacementOutcome Snap(nint window, PixelRect bounds) =>
        TrySnap(window, bounds)
            ? PlacementOutcome.Success()
            : PlacementOutcome.Rejected("Windows hat die Platzierung abgelehnt.");

    /// <summary>
    /// Wie <see cref="Snap"/>, setzt die Groesse aber auch dann, wenn der Fensterstil sie nicht zulaesst.
    /// Gebraucht fuer das Zonen-Vollbild: ein Fenster im Vollbild legt seinen Griffrahmen ab und gaelte
    /// sonst als Fenster fester Groesse, das nur zentriert statt auf die Zone gestreckt wuerde. Die
    /// Vorgabe reicht auf <see cref="Snap"/> durch, damit Fakes in Tests ohne Aenderung weiterlaufen.
    /// </summary>
    PlacementOutcome Fill(nint window, PixelRect bounds) => Snap(window, bounds);

    /// <summary>Ob die linke Maustaste gedrueckt ist; Wachhund fuer einen Ziehvorgang ohne Endereignis.</summary>
    bool IsLeftButtonPressed() => true;

    /// <summary>Ob das Fenster noch existiert; Wachhund fuer ein waehrend des Ziehens zerstoertes Fenster.</summary>
    bool IsWindowAlive(nint window) => true;

    /// <summary>Das Fenster im Vordergrund samt Rechteck, fuer Tastenkuerzel; null ohne geeignetes Fenster.</summary>
    (nint Handle, PixelRect Bounds)? GetForegroundWindow() => null;
}
