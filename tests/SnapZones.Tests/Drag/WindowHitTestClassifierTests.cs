using SnapZones.Windows.Windows;
using Xunit;

namespace SnapZones.Tests.Drag;

public sealed class WindowHitTestClassifierTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(18)]
    public void IsMoveOperation_accepts_non_resize_results_from_a_move_size_event(int hitTest)
    {
        Assert.True(WindowHitTestClassifier.IsMoveOperation(hitTest));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    public void IsMoveOperation_rejects_every_resize_border_result(int hitTest)
    {
        Assert.False(WindowHitTestClassifier.IsMoveOperation(hitTest));
    }
}
