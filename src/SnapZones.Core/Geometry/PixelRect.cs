namespace SnapZones.Core.Geometry;

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(PointInt point) =>
        point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;
}

public readonly record struct PointInt(int X, int Y);
