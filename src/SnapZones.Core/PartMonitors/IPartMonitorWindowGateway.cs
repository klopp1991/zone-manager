using SnapZones.Core.Geometry;

namespace SnapZones.Core.PartMonitors;

public interface IPartMonitorWindowGateway
{
    WindowPlacementSnapshot? Capture(nint windowHandle);
    bool TryApplyNormal(WindowIdentity identity, PixelRect bounds);
    bool TryRestore(WindowPlacementSnapshot snapshot);

    /// <summary>
    /// Wie <see cref="TryApplyNormal"/>, liefert aber zusaetzlich das gemessene Ergebnis und eine
    /// Begruendung. Die Vorgabe reicht das Ja/Nein durch, damit einfache Fakes weiter genuegen.
    /// </summary>
    PlacementOutcome ApplyNormal(WindowIdentity identity, PixelRect bounds) =>
        TryApplyNormal(identity, bounds)
            ? PlacementOutcome.Success()
            : PlacementOutcome.Rejected("Windows hat die Platzierung abgelehnt.");
}
