using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.Core.Monitors;

public sealed record LiveMonitor(
    MonitorIdentity Identity,
    MonitorWorkArea WorkArea,
    uint DpiX,
    uint DpiY,
    bool IsPrimary,
    double? PhysicalWidthCentimeters = null,
    double? PhysicalHeightCentimeters = null,
    PixelRect? Bounds = null)
{
    /// <summary>
    /// Die volle Monitorflaeche einschliesslich Taskleiste. Faellt auf die Arbeitsflaeche zurueck, wenn
    /// die Quelle keine Monitorflaeche kennt (Tests, nicht verbundene Monitore).
    /// </summary>
    public PixelRect MonitorBounds => Bounds ?? new PixelRect(WorkArea.X, WorkArea.Y, WorkArea.Width, WorkArea.Height);
}
