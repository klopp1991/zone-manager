using ZoneManager.Core.Geometry;
using ZoneManager.Core.PartMonitors;
using Xunit;

namespace ZoneManager.Tests.PartMonitors;

public sealed class PlacementHistoryTests
{
    private static readonly WindowIdentity Window = new((nint)42, 100, "TestWindow");

    [Fact]
    public void Remember_keeps_only_configured_depth_and_peeks_newest()
    {
        var history = new PlacementHistory(maxDepth: 2);
        history.Remember(Snapshot(1));
        history.Remember(Snapshot(2));
        history.Remember(Snapshot(3));

        Assert.True(history.TryPeek(Window, out var newest));
        Assert.Equal(3, newest.NormalPosition.X);
        Assert.True(history.DiscardTop(Window));
        Assert.True(history.TryPeek(Window, out var remaining));
        Assert.Equal(2, remaining.NormalPosition.X);
    }

    [Fact]
    public void DiscardTop_does_not_affect_other_window()
    {
        var history = new PlacementHistory();
        var other = new WindowIdentity((nint)43, 100, "TestWindow");
        history.Remember(Snapshot(1));
        history.Remember(Snapshot(2) with { Identity = other });

        Assert.True(history.DiscardTop(Window));
        Assert.False(history.TryPeek(Window, out _));
        Assert.True(history.TryPeek(other, out _));
    }

    private static WindowPlacementSnapshot Snapshot(int x) => new(
        Window,
        Flags: 0,
        ShowCommand: 1,
        MinPosition: new PointInt(-1, -1),
        MaxPosition: new PointInt(-1, -1),
        NormalPosition: new PixelRect(x, 20, 800, 600));
}
