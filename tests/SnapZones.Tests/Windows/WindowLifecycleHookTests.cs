using System.Runtime.InteropServices;
using System.Windows.Forms;
using SnapZones.Windows.Hooks;
using Xunit;

namespace SnapZones.Tests.Windows;

public sealed class WindowLifecycleHookTests
{
    [Fact]
    public void Hook_is_disabled_until_enabled()
    {
        using var hook = new WindowLifecycleHook(new SynchronizationContext());

        Assert.False(hook.IsEnabled);
    }

    [Theory]
    [InlineData(0x8002, WindowLifecycleEventKind.Shown)]
    [InlineData(0x8003, WindowLifecycleEventKind.Hidden)]
    [InlineData(0x8001, WindowLifecycleEventKind.Destroyed)]
    [InlineData(0x800B, WindowLifecycleEventKind.LocationChanged)]
    [InlineData(0x000B, WindowLifecycleEventKind.MoveSizeEnded)]
    [InlineData(0x0017, WindowLifecycleEventKind.MinimizeEnded)]
    public void Map_translates_required_events(uint nativeEvent, WindowLifecycleEventKind expected) =>
        Assert.Equal(expected, WindowLifecycleHook.Map(nativeEvent));

    [Fact]
    public void Hook_receives_a_show_event_from_an_owned_test_window()
    {
        using var hook = new WindowLifecycleHook(new SynchronizationContext());
        using var window = new Form();
        using var received = new ManualResetEventSlim();
        var expectedWindow = window.Handle;
        hook.EventReceived += item =>
        {
            if (item.WindowHandle == expectedWindow && item.Kind == WindowLifecycleEventKind.Shown)
            {
                received.Set();
            }
        };

        hook.Enable();
        NotifyWinEvent(0x8002, expectedWindow, 0, 0);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!received.IsSet && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }

        Assert.True(received.IsSet, "Der Hook hat das Sichtbarkeitsereignis des eigenen Fensters nicht empfangen.");
    }

    [DllImport("user32.dll")]
    private static extern void NotifyWinEvent(uint eventType, nint window, int objectId, int childId);
}
