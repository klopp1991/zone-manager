using SnapZones.Core.Drag;
using Xunit;

namespace SnapZones.Tests.Drag;

public sealed class WindowCandidateEvaluatorTests
{
    [Fact]
    public void IsEligible_accepts_visible_foreign_titlebar_window()
    {
        var snapshot = new WindowSnapshot(true, false, false, false, false, true);

        Assert.True(WindowCandidateEvaluator.IsEligible(snapshot));
    }

    [Fact]
    public void IsEligible_accepts_visible_own_process_titlebar_window()
    {
        var snapshot = new WindowSnapshot(true, false, true, false, false, true);

        Assert.True(WindowCandidateEvaluator.IsEligible(snapshot));
    }

    [Theory]
    [InlineData(false, false, false, false, false, true)]
    [InlineData(true, true, false, false, false, true)]
    [InlineData(true, false, false, true, false, true)]
    [InlineData(true, false, false, false, true, true)]
    [InlineData(true, false, false, false, false, false)]
    public void IsEligible_rejects_unsafe_or_non_titlebar_window(
        bool visible,
        bool child,
        bool ownProcess,
        bool toolWindow,
        bool cloaked,
        bool titleBarDrag)
    {
        var snapshot = new WindowSnapshot(visible, child, ownProcess, toolWindow, cloaked, titleBarDrag);

        Assert.False(WindowCandidateEvaluator.IsEligible(snapshot));
    }
}
