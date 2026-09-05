using SnapZones.Core.Geometry;
using SnapZones.Core.Placement;

namespace SnapZones.Windows.Windows;

public sealed record PlacementWindowSnapshot(
    nint WindowHandle,
    WindowIdentity Identity,
    string Title,
    PixelRect CurrentBounds,
    PixelRect NormalBounds,
    bool IsMaximized,
    bool IsMinimized,
    string? ProcessPath = null,
    /// <summary>
    /// Ob das Programm dieses Fenster von sich aus verschieben darf, und wenn nicht, warum. Siehe
    /// <see cref="AutomaticPlacement"/>. <c>None</c> heisst: erlaubt.
    /// </summary>
    AutomaticPlacementRejection AutomaticPlacementRejection = AutomaticPlacementRejection.None,
    /// <summary>
    /// Ob das Fenster gerade rahmenlos den ganzen Monitor einnimmt — ein Browser oder Videoplayer im
    /// Vollbild. Ein solches Rechteck sagt nichts darüber, wo das Fenster sonst liegt, und gehört nicht
    /// in den Katalog gemerkter Positionen.
    /// </summary>
    bool IsFullscreen = false)
{
    /// <summary>Ob der Auffang und das Wiederherstellen dieses Fenster anfassen duerfen.</summary>
    public bool CanPlaceAutomatically => AutomaticPlacementRejection == AutomaticPlacementRejection.None;
}
