using ZoneManager.Core.Geometry;
using ZoneManager.Core.Placement;

namespace ZoneManager.Windows.Windows;

public sealed record PlacementWindowSnapshot(
    nint WindowHandle,
    WindowIdentity Identity,
    string Title,
    PixelRect CurrentBounds,
    PixelRect NormalBounds,
    bool IsMaximized,
    bool IsMinimized,
    string? ProcessPath = null);
