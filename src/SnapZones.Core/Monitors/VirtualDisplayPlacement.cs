using SnapZones.Core.Geometry;

namespace SnapZones.Core.Monitors;

/// <summary>
/// Wo der virtuelle Monitor im Desktopraum liegt. Windows laesst keine ueberlappenden Monitore zu und
/// rueckt nicht anliegende heran; er kann also nicht «hinter» der Zone liegen. Er kommt rechts neben
/// den Monitor, der am weitesten rechts liegt, oben buendig mit ihm — dort liegt er niemandem im Weg,
/// und der Zeiger kann nur ueber diese eine Kante versehentlich hinueberwandern.
/// </summary>
public static class VirtualDisplayPlacement
{
    public static PointInt ChoosePosition(IEnumerable<PixelRect> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        PixelRect? rightmost = null;
        foreach (var monitor in monitors)
        {
            if (monitor.Width <= 0 || monitor.Height <= 0)
            {
                continue;
            }

            if (rightmost is null ||
                monitor.Right > rightmost.Value.Right ||
                (monitor.Right == rightmost.Value.Right && monitor.Y < rightmost.Value.Y))
            {
                rightmost = monitor;
            }
        }

        return rightmost is { } edge ? new PointInt(edge.Right, edge.Y) : new PointInt(0, 0);
    }
}
