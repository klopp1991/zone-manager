using ZoneManager.Core.Geometry;
using ZoneManager.Windows.Hooks;
using ZoneManager.Windows.Windows;
using ZoneManager.Core.AppRules;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Xunit;

namespace ZoneManager.Tests.Drag;

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
        Assert.Null(service.InspectRuleCandidate(0, Environment.ProcessId));
    }

    [Fact]
    public void WindowRuleHook_observes_created_and_focused_top_level_windows()
    {
        using var hook = new WindowRuleHook(new SynchronizationContext());
        using var created = new ManualResetEventSlim();
        using var focused = new ManualResetEventSlim();
        using var sourceWindow = new Form();
        var expectedWindow = sourceWindow.Handle;
        hook.RuleEvent += (eventType, window) =>
        {
            if (window != expectedWindow)
            {
                return;
            }

            if (eventType == AppRuleEvent.WindowCreated)
            {
                created.Set();
            }
            else if (eventType == AppRuleEvent.WindowFocused)
            {
                focused.Set();
            }
        };

        hook.Enable();
        NativeMethods.NotifyWinEvent(0x8002, expectedWindow, 0, 0);
        NativeMethods.NotifyWinEvent(0x0003, expectedWindow, 0, 0);

        PumpUntil(() => created.IsSet && focused.IsSet);

        Assert.True(created.IsSet, "Der Hook hat das sichtbare neue Fenster nicht gemeldet.");
        Assert.True(focused.IsSet, "Der Hook hat das fokussierte Fenster nicht gemeldet.");
    }

    [Fact]
    public void WindowService_reads_stable_identity_for_an_eligible_top_level_window()
    {
        using var sourceWindow = new Form { Text = "Regeltest" };
        sourceWindow.Show();
        var service = new WindowsWindowService();

        var candidate = service.InspectRuleCandidate(sourceWindow.Handle, ownProcessId: -1);

        Assert.NotNull(candidate);
        Assert.Equal(Environment.ProcessId, candidate.Identity.ProcessId);
        Assert.EndsWith("testhost.exe", candidate.Identity.ProcessPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Regeltest", candidate.Identity.WindowTitle);
        Assert.False(string.IsNullOrWhiteSpace(candidate.Identity.WindowClass));
        sourceWindow.Hide();
    }

    private static void PumpUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern void NotifyWinEvent(uint eventType, nint window, int objectId, int childId);
    }
}
