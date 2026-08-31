namespace ZoneManager.Core.Models;

public sealed record LayoutMetrics(EdgeInsets OuterMargins, int ZoneGap)
{
    public LayoutMetrics(int outerMargin, int zoneGap)
        : this(EdgeInsets.Uniform(outerMargin), zoneGap)
    {
    }

    public int OuterMargin => OuterMargins.Left;

    public static LayoutMetrics Default { get; } = new(EdgeInsets.Uniform(8), 0);
}
