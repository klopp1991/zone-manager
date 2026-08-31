namespace ZoneManager.Core.Geometry;

public readonly record struct MonitorWorkArea(int X, int Y, int Width, int Height)
{
    public bool Contains(PointInt point) =>
        point.X >= X && point.X < X + Width &&
        point.Y >= Y && point.Y < Y + Height;
}
