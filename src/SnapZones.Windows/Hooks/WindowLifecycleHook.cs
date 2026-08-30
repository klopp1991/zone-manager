using System.ComponentModel;
using System.Runtime.InteropServices;
using SnapZones.Core.Drag;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Hooks;

public sealed class WindowLifecycleHook : IWindowLifecycleHook
{
    private const uint Shown = 0x8002;
    private const uint Hidden = 0x8003;
    private const uint Destroyed = 0x8001;
    private const uint LocationChanged = 0x800B;
    private const uint MoveSizeEnded = 0x000B;
    private const uint MinimizeEnded = 0x0017;

    private static readonly uint[] EventTypes = [Shown, Hidden, Destroyed, LocationChanged, MoveSizeEnded, MinimizeEnded];

    private readonly object gate = new();
    private readonly SynchronizationContext synchronizationContext;
    private readonly HookCircuitBreaker circuitBreaker = new(2000, TimeSpan.FromSeconds(10));
    private readonly User32.WinEventProc callback;
    private readonly List<nint> hookHandles = [];
    private bool disposed;

    public WindowLifecycleHook(SynchronizationContext synchronizationContext)
    {
        this.synchronizationContext = synchronizationContext ?? throw new ArgumentNullException(nameof(synchronizationContext));
        callback = OnWinEvent;
    }

    public event Action<WindowLifecycleEvent>? EventReceived;
    public event Action<string>? EmergencyStopped;

    public bool IsEnabled
    {
        get
        {
            lock (gate)
            {
                return hookHandles.Count != 0;
            }
        }
    }

    public void Enable()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (hookHandles.Count != 0)
            {
                return;
            }

            try
            {
                foreach (var eventType in EventTypes)
                {
                    var hookHandle = User32.SetWinEventHook(eventType, eventType, 0, callback, 0, 0, User32.WinEventOutOfContext);
                    if (hookHandle == 0)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Der Fenster-Lebenszyklus-Hook konnte nicht aktiviert werden.");
                    }

                    hookHandles.Add(hookHandle);
                }
            }
            catch
            {
                UnhookAllUnderLock();
                throw;
            }
        }
    }

    public void Disable()
    {
        lock (gate)
        {
            UnhookAllUnderLock();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            UnhookAllUnderLock();
        }

        GC.SuppressFinalize(this);
    }

    public static WindowLifecycleEventKind Map(uint eventType) => eventType switch
    {
        Shown => WindowLifecycleEventKind.Shown,
        Hidden => WindowLifecycleEventKind.Hidden,
        Destroyed => WindowLifecycleEventKind.Destroyed,
        LocationChanged => WindowLifecycleEventKind.LocationChanged,
        MoveSizeEnded => WindowLifecycleEventKind.MoveSizeEnded,
        MinimizeEnded => WindowLifecycleEventKind.MinimizeEnded,
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unbekanntes Fenster-Lebenszyklusereignis.")
    };

    private void OnWinEvent(
        nint hook,
        uint eventType,
        nint window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        _ = hook;
        _ = eventThread;
        _ = eventTime;
        try
        {
            if (window == 0 || objectId != 0 || childId != 0)
            {
                return;
            }

            if (RecordEvent())
            {
                StopForSafety(circuitBreaker.Reason ?? "Der Schutzschalter wurde ausgelöst.");
                return;
            }

            var lifecycleEvent = new WindowLifecycleEvent(window, Map(eventType));
            synchronizationContext.Post(_ => DeliverEvent(lifecycleEvent), null);
        }
        catch (Exception exception)
        {
            TripCircuitBreaker(exception);
            StopForSafety(circuitBreaker.Reason!);
        }
    }

    private bool RecordEvent()
    {
        lock (gate)
        {
            return circuitBreaker.RecordEvent(DateTimeOffset.UtcNow);
        }
    }

    private void DeliverEvent(WindowLifecycleEvent lifecycleEvent)
    {
        try
        {
            EventReceived?.Invoke(lifecycleEvent);
        }
        catch (Exception exception)
        {
            TripCircuitBreaker(exception);
            StopForSafety(circuitBreaker.Reason!);
        }
    }

    private void TripCircuitBreaker(Exception exception)
    {
        lock (gate)
        {
            circuitBreaker.Trip(exception);
        }
    }

    private void StopForSafety(string reason)
    {
        Disable();
        synchronizationContext.Post(_ => EmergencyStopped?.Invoke(reason), null);
    }

    private void UnhookAllUnderLock()
    {
        foreach (var hookHandle in hookHandles)
        {
            _ = User32.UnhookWinEvent(hookHandle);
        }

        hookHandles.Clear();
    }
}
