using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SnapZones.Core.Drag;
using SnapZones.Windows.Hooks;
using SnapZones.Windows.Native;
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

    [Fact]
    public void Enable_registers_six_exact_out_of_context_event_ranges()
    {
        var nativeApi = new TestWinEventHookApi();
        using var hook = CreateHook(nativeApi);

        hook.Enable();

        Assert.Collection(
            nativeApi.Registrations,
            registration => AssertRegistration(registration, 0x8002),
            registration => AssertRegistration(registration, 0x8003),
            registration => AssertRegistration(registration, 0x8001),
            registration => AssertRegistration(registration, 0x800B),
            registration => AssertRegistration(registration, 0x000B),
            registration => AssertRegistration(registration, 0x0017));
    }

    [Fact]
    public void Enable_unhooks_all_acquired_handles_when_a_later_registration_fails()
    {
        var nativeApi = new TestWinEventHookApi { FailingRegistrationNumber = 3 };
        using var hook = CreateHook(nativeApi);

        Assert.Throws<Win32Exception>(hook.Enable);

        Assert.Equal(3, nativeApi.RegistrationAttempts.Count);
        Assert.Equal(new nint[] { 101, 102 }, nativeApi.UnhookedHandles);
        Assert.False(hook.IsEnabled);
    }

    [Fact]
    public void Callback_ignores_non_window_and_non_self_events()
    {
        var nativeApi = new TestWinEventHookApi();
        using var hook = CreateHook(nativeApi);
        var received = new List<WindowLifecycleEvent>();
        hook.EventReceived += received.Add;
        hook.Enable();

        nativeApi.Raise(0x8002, 0, 0, 0);
        nativeApi.Raise(0x8002, 42, 1, 0);
        nativeApi.Raise(0x8002, 42, 0, 1);

        Assert.Empty(received);
    }

    [Fact]
    public void Circuit_breaker_stops_the_hook_and_raises_an_emergency_event()
    {
        var nativeApi = new TestWinEventHookApi();
        using var hook = CreateHook(nativeApi, new HookCircuitBreaker(1, TimeSpan.FromSeconds(10)));
        var received = new List<WindowLifecycleEvent>();
        var emergencyStops = new List<string>();
        hook.EventReceived += received.Add;
        hook.EmergencyStopped += emergencyStops.Add;
        hook.Enable();

        nativeApi.Raise(0x8002, 42, 0, 0);
        nativeApi.Raise(0x8002, 42, 0, 0);

        Assert.Single(received);
        Assert.Single(emergencyStops);
        Assert.False(hook.IsEnabled);
        Assert.Equal(new nint[] { 101, 102, 103, 104, 105, 106 }, nativeApi.UnhookedHandles);
    }

    [Fact]
    public void Enable_disable_and_dispose_are_idempotent_with_exact_unhook_counts()
    {
        var nativeApi = new TestWinEventHookApi();
        var hook = CreateHook(nativeApi);

        hook.Enable();
        hook.Enable();
        hook.Disable();
        hook.Disable();
        hook.Enable();
        hook.Dispose();
        hook.Dispose();

        Assert.Equal(12, nativeApi.Registrations.Count);
        Assert.Equal(
            new nint[] { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112 },
            nativeApi.UnhookedHandles);
    }

    [Fact]
    public void Failed_unhook_stays_logically_disabled_is_retried_and_never_creates_duplicate_hooks()
    {
        var nativeApi = new TestWinEventHookApi();
        using var hook = CreateHook(nativeApi);
        var received = new List<WindowLifecycleEvent>();
        hook.EventReceived += received.Add;
        hook.Enable();
        nativeApi.FailingUnhookHandles.Add(101);

        hook.Disable();
        nativeApi.Raise(0x8002, 42, 0, 0);

        Assert.False(hook.IsEnabled);
        Assert.Empty(received);
        Assert.Throws<Win32Exception>(hook.Enable);
        Assert.Equal(6, nativeApi.Registrations.Count);
        Assert.Equal(2, nativeApi.UnhookedHandles.Count(handle => handle == 101));

        nativeApi.FailingUnhookHandles.Clear();
        hook.Disable();
        hook.Enable();

        Assert.Equal(12, nativeApi.Registrations.Count);
    }

    [Fact]
    public void Dispose_suppresses_queued_and_native_callbacks_even_when_unhook_fails()
    {
        var synchronizationContext = new QueuedSynchronizationContext();
        var nativeApi = new TestWinEventHookApi();
        var hook = new WindowLifecycleHook(
            synchronizationContext,
            nativeApi,
            new HookCircuitBreaker(2000, TimeSpan.FromSeconds(10)));
        var received = new List<WindowLifecycleEvent>();
        hook.EventReceived += received.Add;
        hook.Enable();
        nativeApi.Raise(0x8002, 42, 0, 0);
        nativeApi.FailingUnhookHandles.Add(101);

        hook.Dispose();
        nativeApi.Raise(0x8002, 43, 0, 0);
        synchronizationContext.DeliverAll();

        Assert.Empty(received);
        Assert.False(hook.IsEnabled);
        Assert.Equal(1, nativeApi.UnhookedHandles.Count(handle => handle == 101));
    }

    private static WindowLifecycleHook CreateHook(TestWinEventHookApi nativeApi, HookCircuitBreaker? circuitBreaker = null) =>
        new(new ImmediateSynchronizationContext(), nativeApi, circuitBreaker ?? new HookCircuitBreaker(2000, TimeSpan.FromSeconds(10)));

    private static void AssertRegistration(TestWinEventHookApi.Registration registration, uint expectedEvent)
    {
        Assert.Equal(expectedEvent, registration.EventMinimum);
        Assert.Equal(expectedEvent, registration.EventMaximum);
        Assert.Equal((nint)0, registration.Module);
        Assert.Equal(0U, registration.ProcessId);
        Assert.Equal(0U, registration.ThreadId);
        Assert.Equal(0U, registration.Flags);
    }

    [DllImport("user32.dll")]
    private static extern void NotifyWinEvent(uint eventType, nint window, int objectId, int childId);

    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) => callback(state);
    }

    private sealed class QueuedSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> pending = [];

        public override void Post(SendOrPostCallback callback, object? state) => pending.Enqueue((callback, state));

        public void DeliverAll()
        {
            while (pending.TryDequeue(out var item))
            {
                item.Callback(item.State);
            }
        }
    }

    private sealed class TestWinEventHookApi : IWinEventHookApi
    {
        private nint nextHandle = 101;

        public int FailingRegistrationNumber { get; init; } = int.MaxValue;
        public List<Registration> RegistrationAttempts { get; } = [];
        public List<Registration> Registrations { get; } = [];
        public List<nint> UnhookedHandles { get; } = [];
        public HashSet<nint> FailingUnhookHandles { get; } = [];
        private HashSet<nint> SuccessfullyUnhookedHandles { get; } = [];

        public nint SetWinEventHook(
            uint eventMinimum,
            uint eventMaximum,
            nint module,
            User32.WinEventProc callback,
            uint processId,
            uint threadId,
            uint flags)
        {
            var registration = new Registration(eventMinimum, eventMaximum, module, callback, processId, threadId, flags, 0);
            RegistrationAttempts.Add(registration);
            if (RegistrationAttempts.Count == FailingRegistrationNumber)
            {
                return 0;
            }

            registration = registration with { Handle = nextHandle++ };
            Registrations.Add(registration);
            return registration.Handle;
        }

        public bool UnhookWinEvent(nint hook)
        {
            UnhookedHandles.Add(hook);
            if (FailingUnhookHandles.Contains(hook))
            {
                return false;
            }

            SuccessfullyUnhookedHandles.Add(hook);
            return true;
        }

        public void Raise(uint eventType, nint window, int objectId, int childId)
        {
            foreach (var registration in Registrations.Where(registration =>
                         !SuccessfullyUnhookedHandles.Contains(registration.Handle) &&
                         eventType >= registration.EventMinimum &&
                         eventType <= registration.EventMaximum))
            {
                registration.Callback(registration.Handle, eventType, window, objectId, childId, 0, 0);
            }
        }

        public sealed record Registration(
            uint EventMinimum,
            uint EventMaximum,
            nint Module,
            User32.WinEventProc Callback,
            uint ProcessId,
            uint ThreadId,
            uint Flags,
            nint Handle);
    }
}
