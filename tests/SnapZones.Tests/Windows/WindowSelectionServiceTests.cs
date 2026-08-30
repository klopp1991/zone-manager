using SnapZones.Core.Geometry;
using SnapZones.Core.Placement;
using SnapZones.Windows.Native;
using SnapZones.Windows.Windows;
using Xunit;

namespace SnapZones.Tests.Windows;

public sealed class WindowSelectionServiceTests
{
    [Fact]
    public async Task First_readable_foreground_window_is_returned_and_hook_is_released_once()
    {
        var native = new RecordingHookApi();
        var windows = new RecordingPlacementWindowService();
        windows.ReadableHandles.Add(42);
        var service = new WindowSelectionService(windows, native);
        var selection = service.SelectNextAsync(123, TimeSpan.FromSeconds(2), CancellationToken.None);

        native.Raise(41);
        native.Raise(42);

        Assert.Equal((nint)42, await selection);
        var registration = Assert.Single(native.Registrations);
        Assert.Equal(0x0003u, registration.EventMinimum);
        Assert.Equal(0x0003u, registration.EventMaximum);
        Assert.Equal(User32.WinEventOutOfContext, registration.Flags);
        Assert.Equal([123, 123], windows.ExcludedProcessIds);
        Assert.Equal(1, native.UnhookCalls);
    }

    [Fact]
    public async Task Timeout_returns_zero_and_releases_the_registered_hook_once()
    {
        var native = new RecordingHookApi();
        var service = new WindowSelectionService(new RecordingPlacementWindowService(), native);

        var selected = await service.SelectNextAsync(123, TimeSpan.FromMilliseconds(20), CancellationToken.None);

        Assert.Equal(nint.Zero, selected);
        Assert.Equal(1, native.UnhookCalls);
    }

    [Fact]
    public async Task Cancellation_returns_zero_and_releases_the_registered_hook_once()
    {
        var native = new RecordingHookApi();
        var service = new WindowSelectionService(new RecordingPlacementWindowService(), native);
        using var cancellation = new CancellationTokenSource();
        var selection = service.SelectNextAsync(123, TimeSpan.FromSeconds(2), cancellation.Token);

        cancellation.Cancel();

        Assert.Equal(nint.Zero, await selection);
        Assert.Equal(1, native.UnhookCalls);
    }

    [Fact]
    public async Task Failed_hook_registration_returns_zero_without_unhooking_an_invalid_handle()
    {
        var native = new RecordingHookApi { RegistrationHandle = 0 };
        var service = new WindowSelectionService(new RecordingPlacementWindowService(), native);

        var selected = await service.SelectNextAsync(123, TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.Equal(nint.Zero, selected);
        Assert.Equal(0, native.UnhookCalls);
    }

    [Fact]
    public async Task Failed_unhook_deactivates_the_callback_and_reports_the_cleanup_failure()
    {
        var native = new RecordingHookApi { UnhookResult = false };
        var windows = new RecordingPlacementWindowService();
        windows.ReadableHandles.Add(42);
        var diagnostics = new List<string>();
        var service = new WindowSelectionService(windows, native, diagnostics.Add);

        var selected = await service.SelectNextAsync(123, TimeSpan.FromMilliseconds(20), CancellationToken.None);
        native.Raise(42);

        Assert.Equal(nint.Zero, selected);
        Assert.Empty(windows.ExcludedProcessIds);
        Assert.Single(diagnostics);
        Assert.Contains("Hook", diagnostics[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Failed_unhook_keeps_the_inactive_native_delegate_rooted()
    {
        var native = new RecordingHookApi { UnhookResult = false };
        var service = new WindowSelectionService(new RecordingPlacementWindowService(), native, _ => { });

        _ = await service.SelectNextAsync(123, TimeSpan.Zero, CancellationToken.None);
        native.ReleaseStrongCallback();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.True(native.CallbackReference!.TryGetTarget(out _));
    }

    [Fact]
    public async Task Cleanup_failure_opens_the_shared_circuit_until_the_test_seam_resets_it()
    {
        var circuit = new WindowSelectionHookCircuit();
        var failedNative = new RecordingHookApi { UnhookResult = false };
        var failedService = new WindowSelectionService(
            new RecordingPlacementWindowService(),
            failedNative,
            _ => { },
            circuit);
        _ = await failedService.SelectNextAsync(123, TimeSpan.Zero, CancellationToken.None);
        failedNative.ReleaseStrongCallback();

        var blockedNative = new RecordingHookApi();
        var blockedService = new WindowSelectionService(
            new RecordingPlacementWindowService(),
            blockedNative,
            _ => { },
            circuit);

        Assert.Equal(nint.Zero, await blockedService.SelectNextAsync(123, TimeSpan.Zero, CancellationToken.None));
        Assert.Empty(blockedNative.Registrations);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.True(failedNative.CallbackReference!.TryGetTarget(out _));

        circuit.Reset();
        var resetNative = new RecordingHookApi();
        var resetService = new WindowSelectionService(
            new RecordingPlacementWindowService(),
            resetNative,
            _ => { },
            circuit);
        _ = await resetService.SelectNextAsync(123, TimeSpan.Zero, CancellationToken.None);

        Assert.Single(resetNative.Registrations);
    }

    private sealed class RecordingHookApi : IWinEventHookApi
    {
        private User32.WinEventProc? callback;
        public nint RegistrationHandle { get; set; } = 77;
        public bool UnhookResult { get; set; } = true;
        public List<Registration> Registrations { get; } = [];
        public int UnhookCalls { get; private set; }
        public WeakReference<User32.WinEventProc>? CallbackReference { get; private set; }

        public nint SetWinEventHook(
            uint eventMinimum,
            uint eventMaximum,
            nint module,
            User32.WinEventProc callback,
            uint processId,
            uint threadId,
            uint flags)
        {
            this.callback = callback;
            CallbackReference = new(callback);
            Registrations.Add(new(eventMinimum, eventMaximum, flags));
            return RegistrationHandle;
        }

        public bool UnhookWinEvent(nint hook)
        {
            Assert.Equal(RegistrationHandle, hook);
            UnhookCalls++;
            return UnhookResult;
        }

        public void Raise(nint window)
        {
            var target = callback;
            if (target is null)
            {
                _ = CallbackReference?.TryGetTarget(out target);
            }

            target?.Invoke(RegistrationHandle, 0x0003, window, 0, 0, 0, 0);
        }

        public void ReleaseStrongCallback() => callback = null;

        public sealed record Registration(uint EventMinimum, uint EventMaximum, uint Flags);
    }

    private sealed class RecordingPlacementWindowService : IPlacementWindowService
    {
        public HashSet<nint> ReadableHandles { get; } = [];
        public List<int> ExcludedProcessIds { get; } = [];

        public PlacementWindowSnapshot? Inspect(nint windowHandle, int excludedProcessId)
        {
            ExcludedProcessIds.Add(excludedProcessId);
            return ReadableHandles.Contains(windowHandle)
                ? new PlacementWindowSnapshot(
                    windowHandle,
                    new WindowIdentity("app.exe", "Main", WindowKind.MainWindow),
                    "App",
                    new PixelRect(0, 0, 800, 600),
                    new PixelRect(0, 0, 800, 600),
                    false,
                    false)
                : null;
        }

        public bool TryPlace(nint windowHandle, PixelRect normalBounds, bool maximize) => false;
        public IReadOnlyList<nint> EnumerateEligibleWindows(int excludedProcessId) => [];
        public nint GetForegroundWindow() => 0;
    }
}
