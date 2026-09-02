using SnapZones.Core.Geometry;
using Xunit;

namespace SnapZones.Tests.Geometry;

public sealed class WindowFrameCompensationTests
{
    // Typischer Windows-11-Fensterrahmen bei 100 %: sieben Pixel links, rechts und unten unsichtbar,
    // oben nichts. Das Fensterrechteck ist damit 14 Pixel breiter als der sichtbare Rahmen.
    private static readonly PixelRect WindowRect = new(-7, 0, 1014, 807);
    private static readonly PixelRect VisibleFrame = new(0, 0, 1000, 800);

    [Fact]
    public void Two_adjacent_zones_place_their_visible_frames_without_a_gap()
    {
        var left = new PixelRect(0, 0, 1536, 1400);
        var right = new PixelRect(1536, 0, 1536, 1400);

        var placedLeft = WindowFrameCompensation.Apply(left, WindowRect, VisibleFrame);
        var placedRight = WindowFrameCompensation.Apply(right, WindowRect, VisibleFrame);

        // Die Fensterrechtecke ueberlappen sich nun bewusst um die doppelte Randbreite ...
        Assert.Equal(new PixelRect(-7, 0, 1550, 1407), placedLeft);
        Assert.Equal(new PixelRect(1529, 0, 1550, 1407), placedRight);

        // ... damit die sichtbaren Rahmen exakt aneinanderstossen.
        Assert.Equal(left.Right, placedLeft.Right - 7);
        Assert.Equal(right.X, placedRight.X + 7);
        Assert.Equal(placedLeft.Right - 7, placedRight.X + 7);
    }

    [Fact]
    public void Compensation_grows_the_window_rect_by_the_invisible_border_on_every_side()
    {
        var target = new PixelRect(100, 200, 800, 600);

        var placed = WindowFrameCompensation.Apply(target, WindowRect, VisibleFrame);

        Assert.Equal(new PixelRect(93, 200, 814, 607), placed);
    }

    [Fact]
    public void A_window_without_an_invisible_border_is_placed_unchanged()
    {
        var target = new PixelRect(10, 20, 300, 400);
        var flush = new PixelRect(0, 0, 500, 500);

        Assert.Equal(target, WindowFrameCompensation.Apply(target, flush, flush));
        Assert.False(WindowFrameCompensation.TryMeasure(flush, flush, out _, out _, out _, out _));
    }

    [Theory]
    // Sichtbarer Rahmen groesser als das Fensterrechteck: unmoeglich, also nicht ausgleichen.
    [InlineData(0, 0, 100, 100, -5, -5, 110, 110)]
    // Unplausibel breiter Rand: das Fenster bestimmt seine Groesse selbst.
    [InlineData(0, 0, 500, 500, 100, 100, 300, 300)]
    // Entartete Rechtecke.
    [InlineData(0, 0, 0, 0, 0, 0, 0, 0)]
    public void Implausible_measurements_leave_the_target_untouched(
        int windowX, int windowY, int windowWidth, int windowHeight,
        int frameX, int frameY, int frameWidth, int frameHeight)
    {
        var target = new PixelRect(10, 20, 300, 400);
        var windowRect = new PixelRect(windowX, windowY, windowWidth, windowHeight);
        var frame = new PixelRect(frameX, frameY, frameWidth, frameHeight);

        Assert.Equal(target, WindowFrameCompensation.Apply(target, windowRect, frame));
        Assert.False(WindowFrameCompensation.TryMeasure(windowRect, frame, out _, out _, out _, out _));
    }

    [Fact]
    public void Measured_borders_are_reported_per_side()
    {
        Assert.True(WindowFrameCompensation.TryMeasure(
            WindowRect,
            VisibleFrame,
            out var left,
            out var top,
            out var right,
            out var bottom));

        Assert.Equal(7, left);
        Assert.Equal(0, top);
        Assert.Equal(7, right);
        Assert.Equal(7, bottom);
    }

    [Fact]
    public void A_border_at_the_documented_limit_is_still_compensated()
    {
        var windowRect = new PixelRect(0, 0, 600, 600);
        var frame = new PixelRect(
            WindowFrameCompensation.MaximumBorderPixels,
            WindowFrameCompensation.MaximumBorderPixels,
            600 - 2 * WindowFrameCompensation.MaximumBorderPixels,
            600 - 2 * WindowFrameCompensation.MaximumBorderPixels);

        Assert.True(WindowFrameCompensation.TryMeasure(windowRect, frame, out var left, out _, out _, out _));
        Assert.Equal(WindowFrameCompensation.MaximumBorderPixels, left);
    }
}
