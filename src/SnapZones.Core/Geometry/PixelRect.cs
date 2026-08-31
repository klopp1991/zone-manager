namespace SnapZones.Core.Geometry;

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(PointInt point) =>
        point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;

    public bool Contains(PixelRect rectangle) =>
        rectangle.Width > 0 && rectangle.Height > 0 &&
        rectangle.X >= X && rectangle.Y >= Y &&
        rectangle.Right <= Right && rectangle.Bottom <= Bottom;
}

public readonly record struct PointInt(int X, int Y);
