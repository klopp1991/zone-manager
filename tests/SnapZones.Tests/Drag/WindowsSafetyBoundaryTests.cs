using SnapZones.Core.Geometry;
using SnapZones.Windows.Hooks;
using SnapZones.Windows.Windows;
using Xunit;

namespace SnapZones.Tests.Drag;

public sealed class WindowsSafetyBoundaryTests
{
    [Fact]
    public void WindowMoveHook_is_disabled_until_explicit_enable()
    {
        using var hook = new WindowMoveHook(new SynchronizationContext());

        Assert.False(hook.IsEnabled);
    }

    [Fact]
    public void WindowService_rejects_invalid_handle_without_side_effects()
    {
        var service = new WindowsWindowService();

        Assert.Null(service.Inspect(0, new PointInt(0, 0), Environment.ProcessId));
        Assert.False(service.TrySnap(0, new PixelRect(0, 0, 800, 600)));
    }
}
