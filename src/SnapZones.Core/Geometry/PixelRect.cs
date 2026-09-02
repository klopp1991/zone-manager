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

    /// <summary>
    /// Ob alle vier Kanten hoechstens <paramref name="tolerance"/> Pixel von denen des anderen Rechtecks
    /// abweichen. Fenster liegen wegen des unsichtbaren Griffbereichs nie exakt auf einer Zone; ein
    /// Vergleich auf Gleichheit oder vollstaendiges Enthaltensein verfehlt sie deshalb.
    /// </summary>
    public bool IsWithinTolerance(PixelRect other, int tolerance) =>
        Width > 0 && Height > 0 && other.Width > 0 && other.Height > 0 &&
        Math.Abs(X - other.X) <= tolerance &&
        Math.Abs(Y - other.Y) <= tolerance &&
        Math.Abs(Right - other.Right) <= tolerance &&
        Math.Abs(Bottom - other.Bottom) <= tolerance;

    /// <summary>
    /// Ein Rechteck dieser Groesse, mittig in <paramref name="container"/> gelegt. Ist es groesser als der
    /// Behaelter, wird es an dessen linker oberer Ecke ausgerichtet statt darueber hinauszuragen.
    /// Gebraucht fuer Fenster ohne veraenderbare Groesse, die eine Zone nicht fuellen koennen.
    /// </summary>
    public PixelRect CenteredIn(PixelRect container)
    {
        var x = container.X + Math.Max(0, (container.Width - Width) / 2);
        var y = container.Y + Math.Max(0, (container.Height - Height) / 2);
        return new PixelRect(x, y, Width, Height);
    }

    /// <summary>Quadrat des Abstands eines Punkts zum Rechteck; null, wenn der Punkt darin liegt.</summary>
    public long DistanceSquaredTo(PointInt point)
    {
        long dx = point.X < X ? X - point.X : point.X >= Right ? point.X - (Right - 1) : 0;
        long dy = point.Y < Y ? Y - point.Y : point.Y >= Bottom ? point.Y - (Bottom - 1) : 0;
        return dx * dx + dy * dy;
    }
}

public readonly record struct PointInt(int X, int Y);
