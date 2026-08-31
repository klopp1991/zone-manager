using System.Windows;
using ZoneManager.App.Controls;
using ZoneManager.Core.Geometry;
using ZoneManager.Core.Models;
using Xunit;

namespace ZoneManager.Tests.Theme;

public sealed class LayoutCanvasInteractionTests
{
    [Fact]
    public void HitTestZone_prefers_selected_right_zone_at_its_visible_left_handle()
    {
        var rightId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var leftId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var zones = new[]
        {
            new ZoneDefinition(rightId, "Rechts", new NormalizedRect(0.5, 0, 0.5, 1)),
            new ZoneDefinition(leftId, "Links", new NormalizedRect(0, 0, 0.5, 1))
        };

        var hit = LayoutCanvasInteraction.HitTestZone(
            zones,
            rightId,
            new Rect(0, 0, 1000, 500),
            new Point(496, 250));

        Assert.Equal(rightId, hit?.Id);
    }

    [Theory]
    [InlineData(96, 160, ZoneEdges.Left)]
    [InlineData(304, 160, ZoneEdges.Right)]
    [InlineData(200, 96, ZoneEdges.Top)]
    [InlineData(200, 224, ZoneEdges.Bottom)]
    [InlineData(96, 96, ZoneEdges.Left | ZoneEdges.Top)]
    [InlineData(304, 224, ZoneEdges.Right | ZoneEdges.Bottom)]
    [InlineData(200, 160, ZoneEdges.None)]
    public void DetectResizeEdges_recognises_every_handle(double x, double y, ZoneEdges expected)
    {
        var result = LayoutCanvasInteraction.DetectResizeEdges(
            new Rect(100, 100, 200, 120),
            new Point(x, y));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void DetectResizeEdges_chooses_the_nearest_opposite_edge_for_a_small_zone()
    {
        var result = LayoutCanvasInteraction.DetectResizeEdges(
            new Rect(100, 100, 20, 20),
            new Point(117, 112));

        Assert.Equal(ZoneEdges.Right | ZoneEdges.Bottom, result);
    }

    [Fact]
    public void Transform_extends_right_zone_to_the_left_without_moving_its_right_edge()
    {
        var original = new NormalizedRect(0.5, 0, 0.5, 1);

        var result = LayoutCanvasInteraction.Transform(original, -0.1, 0, ZoneEdges.Left);

        Assert.Equal(new NormalizedRect(0.4, 0, 0.6, 1), result);
    }

    [Theory]
    [InlineData(ZoneEdges.Left, -0.1, 0, 0.4, 0.25, 0.35, 0.5)]
    [InlineData(ZoneEdges.Right, 0.1, 0, 0.5, 0.25, 0.35, 0.5)]
    [InlineData(ZoneEdges.Top, 0, -0.1, 0.5, 0.15, 0.25, 0.6)]
    [InlineData(ZoneEdges.Bottom, 0, 0.1, 0.5, 0.25, 0.25, 0.6)]
    [InlineData(ZoneEdges.Left | ZoneEdges.Top, -0.1, -0.1, 0.4, 0.15, 0.35, 0.6)]
    [InlineData(ZoneEdges.Right | ZoneEdges.Bottom, 0.1, 0.1, 0.5, 0.25, 0.35, 0.6)]
    public void Transform_resizes_every_edge_and_corner(
        ZoneEdges edges,
        double deltaX,
        double deltaY,
        double expectedX,
        double expectedY,
        double expectedWidth,
        double expectedHeight)
    {
        var original = new NormalizedRect(0.5, 0.25, 0.25, 0.5);

        var result = LayoutCanvasInteraction.Transform(original, deltaX, deltaY, edges);

        Assert.Equal(
            new NormalizedRect(expectedX, expectedY, expectedWidth, expectedHeight),
            result);
    }

    [Fact]
    public void Transform_moves_zone_without_changing_its_size()
    {
        var original = new NormalizedRect(0.5, 0.25, 0.25, 0.5);

        var result = LayoutCanvasInteraction.Transform(original, -0.1, 0.1, ZoneEdges.None);

        Assert.Equal(new NormalizedRect(0.4, 0.35, 0.25, 0.5), result);
    }

    [Fact]
    public void ApplyDrag_resizes_left_edge_and_snaps_it_pixel_exactly_to_neighbour()
    {
        var original = new NormalizedRect(0.5, 0, 0.5, 1);
        var neighbour = new NormalizedRect(0, 0, 0.4, 1);

        var result = LayoutCanvasInteraction.ApplyDrag(
            original,
            -0.093,
            0,
            ZoneEdges.Left,
            [neighbour],
            20,
            1920,
            1080,
            false);

        Assert.Equal(new NormalizedRect(0.4, 0, 0.6, 1), result.Bounds);
        Assert.Equal(ZoneEdges.Left, result.SnappedEdges);
    }

    [Fact]
    public void ApplyDrag_moves_and_snaps_two_axes_while_preserving_size()
    {
        var original = new NormalizedRect(0.7, 0.7, 0.2, 0.2);
        var neighbour = new NormalizedRect(0, 0, 0.5, 0.5);

        var result = LayoutCanvasInteraction.ApplyDrag(
            original,
            -0.193,
            -0.193,
            ZoneEdges.None,
            [neighbour],
            20,
            1920,
            1080,
            false);

        Assert.Equal(new NormalizedRect(0.5, 0.5, 0.2, 0.2), result.Bounds);
        Assert.Equal(ZoneEdges.Left | ZoneEdges.Top, result.SnappedEdges);
    }

    [Fact]
    public void ApplyDrag_with_alt_pause_keeps_unsnapped_position_and_hides_guides()
    {
        var original = new NormalizedRect(0.5, 0, 0.5, 1);
        var neighbour = new NormalizedRect(0, 0, 0.4, 1);

        var result = LayoutCanvasInteraction.ApplyDrag(
            original,
            -0.093,
            0,
            ZoneEdges.Left,
            [neighbour],
            20,
            1920,
            1080,
            true);

        Assert.Equal(0.407, result.Bounds.X, 12);
        Assert.Equal(0, result.Bounds.Y);
        Assert.Equal(0.593, result.Bounds.Width, 12);
        Assert.Equal(1, result.Bounds.Height);
        Assert.Equal(ZoneEdges.None, result.SnappedEdges);
    }

    [Fact]
    public void GetSnapGuides_returns_a_pixel_aligned_line_for_every_snapped_edge()
    {
        var screen = new Rect(10, 20, 1000, 500);
        var bounds = new NormalizedRect(0.25, 0.2, 0.5, 0.6);

        var result = LayoutCanvasInteraction.GetSnapGuides(
            bounds,
            screen,
            ZoneEdges.Left | ZoneEdges.Top | ZoneEdges.Right | ZoneEdges.Bottom);

        Assert.Equal(
            [
                new SnapGuide(new Point(260, 20), new Point(260, 520)),
                new SnapGuide(new Point(760, 20), new Point(760, 520)),
                new SnapGuide(new Point(10, 120), new Point(1010, 120)),
                new SnapGuide(new Point(10, 420), new Point(1010, 420))
            ],
            result);
    }

    [Fact]
    public void GetSnapGuides_returns_none_without_an_active_snap()
    {
        var result = LayoutCanvasInteraction.GetSnapGuides(
            NormalizedRect.Full,
            new Rect(0, 0, 1000, 500),
            ZoneEdges.None);

        Assert.Empty(result);
    }

    [Fact]
    public void FindSharedDivider_finds_the_exact_vertical_boundary_under_the_pointer()
    {
        var left = Zone("11111111-1111-1111-1111-111111111111", "Links", 0, 0, 0.5, 1);
        var right = Zone("22222222-2222-2222-2222-222222222222", "Rechts", 0.5, 0, 0.5, 1);

        var result = LayoutCanvasInteraction.FindSharedDivider(
            [left, right],
            new Rect(0, 0, 1000, 500),
            new Point(500, 250));

        Assert.NotNull(result);
        Assert.Equal(SharedDividerOrientation.Vertical, result.Orientation);
        Assert.Equal(left.Id, result.BeforeZone.Id);
        Assert.Equal(right.Id, result.AfterZone.Id);
        Assert.Equal(0.5, result.Boundary);
        Assert.Equal(0, result.SegmentStart);
        Assert.Equal(1, result.SegmentEnd);
    }

    [Fact]
    public void FindSharedDivider_finds_the_exact_horizontal_boundary_under_the_pointer()
    {
        var top = Zone("11111111-1111-1111-1111-111111111111", "Oben", 0, 0, 1, 0.5);
        var bottom = Zone("22222222-2222-2222-2222-222222222222", "Unten", 0, 0.5, 1, 0.5);

        var result = LayoutCanvasInteraction.FindSharedDivider(
            [top, bottom],
            new Rect(0, 0, 1000, 500),
            new Point(500, 250));

        Assert.NotNull(result);
        Assert.Equal(SharedDividerOrientation.Horizontal, result.Orientation);
        Assert.Equal(top.Id, result.BeforeZone.Id);
        Assert.Equal(bottom.Id, result.AfterZone.Id);
    }

    [Fact]
    public void FindSharedDivider_at_a_t_junction_selects_the_pair_beneath_the_pointer()
    {
        var left = Zone("11111111-1111-1111-1111-111111111111", "Links", 0, 0, 0.5, 1);
        var topRight = Zone("22222222-2222-2222-2222-222222222222", "Rechts oben", 0.5, 0, 0.5, 0.5);
        var bottomRight = Zone("33333333-3333-3333-3333-333333333333", "Rechts unten", 0.5, 0.5, 0.5, 0.5);

        var result = LayoutCanvasInteraction.FindSharedDivider(
            [left, topRight, bottomRight],
            new Rect(0, 0, 1000, 500),
            new Point(500, 125));

        Assert.NotNull(result);
        Assert.Equal(left.Id, result.BeforeZone.Id);
        Assert.Equal(topRight.Id, result.AfterZone.Id);
        Assert.Equal(0, result.SegmentStart);
        Assert.Equal(0.5, result.SegmentEnd);
    }

    [Fact]
    public void FindSharedDivider_ignores_zones_with_a_gap_between_them()
    {
        var left = Zone("11111111-1111-1111-1111-111111111111", "Links", 0, 0, 0.499, 1);
        var right = Zone("22222222-2222-2222-2222-222222222222", "Rechts", 0.5, 0, 0.5, 1);

        var result = LayoutCanvasInteraction.FindSharedDivider(
            [left, right],
            new Rect(0, 0, 1000, 500),
            new Point(500, 250));

        Assert.Null(result);
    }

    [Fact]
    public void ResizeSharedDivider_moves_a_vertical_boundary_and_keeps_both_zones_connected()
    {
        var left = Zone("11111111-1111-1111-1111-111111111111", "Links", 0, 0, 0.5, 1);
        var right = Zone("22222222-2222-2222-2222-222222222222", "Rechts", 0.5, 0, 0.5, 1);
        var divider = new SharedZoneDivider(left, right, SharedDividerOrientation.Vertical, 0.5, 0, 1);

        var result = LayoutCanvasInteraction.ResizeSharedDivider(divider, 0.1);

        Assert.Equal(new NormalizedRect(0, 0, 0.6, 1), result[left.Id]);
        Assert.Equal(new NormalizedRect(0.6, 0, 0.4, 1), result[right.Id]);
    }

    [Fact]
    public void ResizeSharedDivider_moves_a_horizontal_boundary_and_keeps_both_zones_connected()
    {
        var top = Zone("11111111-1111-1111-1111-111111111111", "Oben", 0, 0, 1, 0.5);
        var bottom = Zone("22222222-2222-2222-2222-222222222222", "Unten", 0, 0.5, 1, 0.5);
        var divider = new SharedZoneDivider(top, bottom, SharedDividerOrientation.Horizontal, 0.5, 0, 1);

        var result = LayoutCanvasInteraction.ResizeSharedDivider(divider, -0.1);

        Assert.Equal(new NormalizedRect(0, 0, 1, 0.4), result[top.Id]);
        Assert.Equal(new NormalizedRect(0, 0.4, 1, 0.6), result[bottom.Id]);
    }

    [Theory]
    [InlineData(-0.8, 0.04, 0.96)]
    [InlineData(0.8, 0.96, 0.04)]
    public void ResizeSharedDivider_preserves_the_minimum_width_of_both_zones(
        double delta,
        double expectedLeftWidth,
        double expectedRightWidth)
    {
        var left = Zone("11111111-1111-1111-1111-111111111111", "Links", 0, 0, 0.5, 1);
        var right = Zone("22222222-2222-2222-2222-222222222222", "Rechts", 0.5, 0, 0.5, 1);
        var divider = new SharedZoneDivider(left, right, SharedDividerOrientation.Vertical, 0.5, 0, 1);

        var result = LayoutCanvasInteraction.ResizeSharedDivider(divider, delta);

        Assert.Equal(expectedLeftWidth, result[left.Id].Width, 12);
        Assert.Equal(expectedRightWidth, result[right.Id].Width, 12);
        Assert.Equal(result[left.Id].X + result[left.Id].Width, result[right.Id].X, 12);
    }

    [Fact]
    public void GetSharedDividerVisual_draws_a_vertical_line_and_centres_the_handle_on_the_pointer()
    {
        var left = Zone("11111111-1111-1111-1111-111111111111", "Links", 0, 0.2, 0.5, 0.6);
        var right = Zone("22222222-2222-2222-2222-222222222222", "Rechts", 0.5, 0.2, 0.5, 0.6);
        var divider = new SharedZoneDivider(left, right, SharedDividerOrientation.Vertical, 0.5, 0.2, 0.8);

        var result = LayoutCanvasInteraction.GetSharedDividerVisual(
            divider,
            new Rect(0, 0, 1000, 500),
            new Point(500, 200));

        Assert.Equal(new SnapGuide(new Point(500, 100), new Point(500, 400)), result.Line);
        Assert.Equal(new Rect(493, 180, 14, 40), result.Handle);
    }

    [Fact]
    public void GetSharedDividerVisual_draws_a_horizontal_line_and_centres_the_handle_on_the_pointer()
    {
        var top = Zone("11111111-1111-1111-1111-111111111111", "Oben", 0.2, 0, 0.6, 0.5);
        var bottom = Zone("22222222-2222-2222-2222-222222222222", "Unten", 0.2, 0.5, 0.6, 0.5);
        var divider = new SharedZoneDivider(top, bottom, SharedDividerOrientation.Horizontal, 0.5, 0.2, 0.8);

        var result = LayoutCanvasInteraction.GetSharedDividerVisual(
            divider,
            new Rect(0, 0, 1000, 500),
            new Point(400, 250));

        Assert.Equal(new SnapGuide(new Point(200, 250), new Point(800, 250)), result.Line);
        Assert.Equal(new Rect(380, 243, 40, 14), result.Handle);
    }

    private static ZoneDefinition Zone(
        string id,
        string name,
        double x,
        double y,
        double width,
        double height) =>
        new(Guid.Parse(id), name, new NormalizedRect(x, y, width, height));
}
