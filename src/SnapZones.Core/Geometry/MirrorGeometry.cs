namespace SnapZones.Core.Geometry;

/// <summary>
/// Wie das Bild des virtuellen Monitors in der Zone liegt: welcher Ausschnitt der Quelle an welche
/// Stelle des Ziels kopiert wird. Sind beide gleich gross, ist es die ganze Flaeche; ist der Monitor
/// kleiner, bekommt das Bild einen Rand und liegt mittig; ist er groesser, wird mittig beschnitten.
/// </summary>
public readonly record struct MirrorPlan(int SourceX, int SourceY, int DestinationX, int DestinationY, int Width, int Height)
{
    public bool IsExact => SourceX == 0 && SourceY == 0 && DestinationX == 0 && DestinationY == 0;
}

public static class MirrorGeometry
{
    public static MirrorPlan Plan(int zoneWidth, int zoneHeight, int virtualWidth, int virtualHeight)
    {
        var width = Math.Max(0, Math.Min(zoneWidth, virtualWidth));
        var height = Math.Max(0, Math.Min(zoneHeight, virtualHeight));
        return new MirrorPlan(
            Math.Max(0, (virtualWidth - zoneWidth) / 2),
            Math.Max(0, (virtualHeight - zoneHeight) / 2),
            Math.Max(0, (zoneWidth - virtualWidth) / 2),
            Math.Max(0, (zoneHeight - virtualHeight) / 2),
            width,
            height);
    }

    /// <summary>Ein Punkt in der Zone (Desktopkoordinaten) als Punkt auf dem virtuellen Monitor.</summary>
    public static PointInt ToVirtual(MirrorPlan plan, PixelRect zone, PixelRect virtualBounds, PointInt zonePoint) => new(
        virtualBounds.X + plan.SourceX + Clamp(zonePoint.X - zone.X - plan.DestinationX, plan.Width),
        virtualBounds.Y + plan.SourceY + Clamp(zonePoint.Y - zone.Y - plan.DestinationY, plan.Height));

    /// <summary>Ein Punkt auf dem virtuellen Monitor als Punkt in der Zone (Desktopkoordinaten).</summary>
    public static PointInt ToZone(MirrorPlan plan, PixelRect zone, PixelRect virtualBounds, PointInt virtualPoint) => new(
        zone.X + plan.DestinationX + Clamp(virtualPoint.X - virtualBounds.X - plan.SourceX, plan.Width),
        zone.Y + plan.DestinationY + Clamp(virtualPoint.Y - virtualBounds.Y - plan.SourceY, plan.Height));

    private static int Clamp(int value, int length) => Math.Clamp(value, 0, Math.Max(0, length - 1));
}
