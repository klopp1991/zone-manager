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
    string? ProcessPath = null);
