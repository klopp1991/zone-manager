namespace SnapZones.Core.Geometry;

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(PointInt point) =>
        point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;

    /// <summary>
    /// Das kleinste Rechteck, das beide umschliesst. Wird gebraucht, wenn ein Fenster ueber mehrere
    /// Zonen gezogen wird: Ziel ist dann die Huellbox der ausgewaehlten Zonen. Liegen die Zonen nicht
    /// aneinander, deckt die Huellbox auch die Luecke dazwischen ab; das ist beabsichtigt, weil ein
    /// Fenster nur ein Rechteck einnehmen kann.
    /// </summary>
    public PixelRect Union(PixelRect other)
    {
        if (Width <= 0 || Height <= 0)
        {
            return other;
        }

        if (other.Width <= 0 || other.Height <= 0)
        {
            return this;
        }

        var left = Math.Min(X, other.X);
        var top = Math.Min(Y, other.Y);
        var right = Math.Max(Right, other.Right);
        var bottom = Math.Max(Bottom, other.Bottom);
        return new PixelRect(left, top, right - left, bottom - top);
    }

    public bool Contains(PixelRect rectangle) =>
        rectangle.Width > 0 && rectangle.Height > 0 &&
        rectangle.X >= X && rectangle.Y >= Y &&
        rectangle.Right <= Right && rectangle.Bottom <= Bottom;
}

public readonly record struct PointInt(int X, int Y);
