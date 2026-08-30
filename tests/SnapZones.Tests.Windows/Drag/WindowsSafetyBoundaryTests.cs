using SnapZones.Core.Geometry;
using SnapZones.Windows.Hooks;
using SnapZones.Windows.Windows;
using System.Runtime.InteropServices;
using System.Windows.Forms;
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
    public void WindowMoveHook_observes_move_events_raised_by_own_process()
    {
        using var hook = new WindowMoveHook(new SynchronizationContext());
        using var received = new ManualResetEventSlim();
        using var sourceWindow = new Form();
        var expectedWindow = sourceWindow.Handle;
        hook.MoveStarted += window =>
        {
            if (window == expectedWindow)
            {
                received.Set();
            }
        };

        hook.Enable();
        NativeMethods.NotifyWinEvent(0x000A, expectedWindow, 0, 0);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!received.IsSet && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }

        Assert.True(received.IsSet, "Der Hook hat das Verschiebeereignis des eigenen Prozesses nicht empfangen.");
    }

    [Fact]
    public void WindowService_rejects_invalid_handle_without_side_effects()
    {
        var service = new WindowsWindowService();

        Assert.Null(service.Inspect(0, new PointInt(0, 0), Environment.ProcessId));
        Assert.False(service.TrySnap(0, new PixelRect(0, 0, 800, 600)));
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern void NotifyWinEvent(uint eventType, nint window, int objectId, int childId);
    }
}
