namespace SnapZones.Core.Models;

public sealed record LayoutMetrics(int OuterMargin, int ZoneGap)
{
    public static LayoutMetrics Default { get; } = new(8, 8);
}
