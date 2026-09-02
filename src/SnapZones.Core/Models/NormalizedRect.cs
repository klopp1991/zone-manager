namespace SnapZones.Core.Models;

public sealed record NormalizedRect(double X, double Y, double Width, double Height)
{
    public static NormalizedRect Full { get; } = new(0, 0, 1, 1);
}
