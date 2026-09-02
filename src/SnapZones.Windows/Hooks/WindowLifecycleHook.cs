using System.ComponentModel;
using System.Collections.Concurrent;
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
    private const uint Focused = 0x0003;

    private static readonly uint[] EventTypes = [Shown, Hidden, Destroyed, LocationChanged, MoveSizeEnded, MinimizeEnded, Focused];
    private static readonly ConcurrentDictionary<nint, User32.WinEventProc> OrphanedCallbacks = new();

    private readonly object gate = new();
    private readonly SynchronizationContext synchronizationContext;
    private readonly IWinEventHookApi nativeApi;
    private readonly HookCircuitBreaker circuitBreaker;
    private readonly User32.WinEventProc callback;
    private readonly List<nint> hookHandles = [];
    private long eventGeneration;
    private bool acceptingEvents;
    private bool disposed;

    public WindowLifecycleHook(SynchronizationContext synchronizationContext)
        : this(
            synchronizationContext,
            new User32WinEventHookApi(),
            new HookCircuitBreaker(2000, TimeSpan.FromSeconds(10)))
    {
    }

    internal WindowLifecycleHook(
        SynchronizationContext synchronizationContext,
        IWinEventHookApi nativeApi,
        HookCircuitBreaker circuitBreaker)
    {
        this.synchronizationContext = synchronizationContext ?? throw new ArgumentNullException(nameof(synchronizationContext));
        this.nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        this.circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
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
                return acceptingEvents;
            }
        }
    }

    public void Enable()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (acceptingEvents)
            {
                return;
            }

            circuitBreaker.Reset();
            if (hookHandles.Count != 0)
            {
                UnhookAllUnderLock();
                if (hookHandles.Count != 0)
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "Ein vorheriger Fenster-Lebenszyklus-Hook konnte nicht entfernt werden.");
                }
            }

            try
            {
                foreach (var eventType in EventTypes)
                {
                    var hookHandle = nativeApi.SetWinEventHook(eventType, eventType, 0, callback, 0, 0, User32.WinEventOutOfContext);
                    if (hookHandle == 0)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Der Fenster-Lebenszyklus-Hook konnte nicht aktiviert werden.");
                    }

                    hookHandles.Add(hookHandle);
                }

                acceptingEvents = true;
                eventGeneration++;
            }
            catch
            {
                acceptingEvents = false;
                eventGeneration++;
                UnhookAllUnderLock();
                throw;
            }
        }
    }

    public void Disable()
    {
        lock (gate)
        {
            acceptingEvents = false;
            eventGeneration++;
            UnhookAllUnderLock();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                UnhookAllUnderLock();
                RootRemainingCallbacksUnderLock();
                return;
            }

            disposed = true;
            acceptingEvents = false;
            eventGeneration++;
            UnhookAllUnderLock();
            RootRemainingCallbacksUnderLock();
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
        Focused => WindowLifecycleEventKind.Focused,
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

            long generation;
            bool stopForSafety;
            lock (gate)
            {
                if (disposed || !acceptingEvents || !hookHandles.Contains(hook))
                {
                    return;
                }

                generation = eventGeneration;
                stopForSafety = circuitBreaker.RecordEvent(DateTimeOffset.UtcNow);
            }

            if (stopForSafety)
            {
                StopForSafety(circuitBreaker.Reason ?? "Der Schutzschalter wurde ausgelöst.");
                return;
            }

            var lifecycleEvent = new WindowLifecycleEvent(window, Map(eventType));
            synchronizationContext.Post(_ => DeliverEvent(lifecycleEvent, generation), null);
        }
        catch (Exception exception)
        {
            TripCircuitBreaker(exception);
            StopForSafety(circuitBreaker.Reason!);
        }
    }

    private void DeliverEvent(WindowLifecycleEvent lifecycleEvent, long generation)
    {
        lock (gate)
        {
            if (disposed || !acceptingEvents || eventGeneration != generation)
            {
                return;
            }
        }

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
        synchronizationContext.Post(_ => DeliverEmergencyStop(reason), null);
    }

    private void DeliverEmergencyStop(string reason)
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
        }

        EmergencyStopped?.Invoke(reason);
    }

    private void UnhookAllUnderLock()
    {
        var retainedHandles = new List<nint>();
        foreach (var hookHandle in hookHandles)
        {
            if (nativeApi.UnhookWinEvent(hookHandle))
            {
                OrphanedCallbacks.TryRemove(hookHandle, out _);
            }
            else
            {
                retainedHandles.Add(hookHandle);
            }
        }

        hookHandles.Clear();
        hookHandles.AddRange(retainedHandles);
    }

    private void RootRemainingCallbacksUnderLock()
    {
        foreach (var hookHandle in hookHandles)
        {
            OrphanedCallbacks[hookHandle] = callback;
        }
    }
}
