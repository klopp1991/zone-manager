using System.ComponentModel;
using System.Runtime.InteropServices;
using SnapZones.Core.Drag;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Hooks;

public sealed class WindowMoveHook : IWindowMoveHook
{
    private const uint MoveSizeStart = 0x000A;
    private const uint MoveSizeEnd = 0x000B;
    private const uint OutOfContext = 0x0000;
    private readonly SynchronizationContext synchronizationContext;
    private readonly Action<string>? trace;
    // Jeder Ziehvorgang liefert zwei Ereignisse. 400 in zehn Sekunden erreicht kein Mensch von Hand;
    // die alte Grenze von 100 war mit zuegigem Fensterschieben erreichbar und schaltete das Einrasten
    // bis zum Neustart ab.
    private readonly HookCircuitBreaker circuitBreaker = new(400, TimeSpan.FromSeconds(10));
    private readonly User32.WinEventProc callback;
    private nint hookHandle;
    private bool disposed;

    public WindowMoveHook(SynchronizationContext synchronizationContext, Action<string>? trace = null)
    {
        this.synchronizationContext = synchronizationContext ?? throw new ArgumentNullException(nameof(synchronizationContext));
        this.trace = trace;
        callback = OnWinEvent;
    }

    public event Action<nint>? MoveStarted;
    public event Action<nint>? MoveEnded;
    public event Action<string>? EmergencyStopped;

    public bool IsEnabled => hookHandle != 0;

    public void Enable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsEnabled)
        {
            return;
        }

        circuitBreaker.Reset();
        hookHandle = User32.SetWinEventHook(
            MoveSizeStart,
            MoveSizeEnd,
            0,
            callback,
            0,
            0,
            OutOfContext);
        if (hookHandle == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Der Fenster-Hook konnte nicht aktiviert werden.");
        }
    }

    public void Disable()
    {
        var handle = Interlocked.Exchange(ref hookHandle, 0);
        if (handle != 0)
        {
            _ = User32.UnhookWinEvent(handle);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Disable();
        GC.SuppressFinalize(this);
    }

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
        _ = childId;
        _ = eventThread;
        _ = eventTime;
        try
        {
            trace?.Invoke($"Fensterereignis event=0x{eventType:X4} hwnd=0x{window:X} objectId={objectId} childId={childId}");
            if (window == 0 || objectId != 0)
            {
                trace?.Invoke("Fensterereignis ignoriert: kein Top-Level-Fenster.");
                return;
            }

            if (circuitBreaker.RecordEvent(DateTimeOffset.UtcNow))
            {
                StopForSafety(circuitBreaker.Reason ?? "Der Schutzschalter wurde ausgelöst.");
                return;
            }

            synchronizationContext.Post(_ =>
            {
                try
                {
                    if (eventType == MoveSizeStart)
                    {
                        MoveStarted?.Invoke(window);
                    }
                    else if (eventType == MoveSizeEnd)
                    {
                        MoveEnded?.Invoke(window);
                    }
                }
                catch (Exception exception)
                {
                    circuitBreaker.Trip(exception);
                    StopForSafety(circuitBreaker.Reason!);
                }
            }, null);
        }
        catch (Exception exception)
        {
            circuitBreaker.Trip(exception);
            StopForSafety(circuitBreaker.Reason!);
        }
    }

    private void StopForSafety(string reason)
    {
        Disable();
        synchronizationContext.Post(_ => EmergencyStopped?.Invoke(reason), null);
    }
}
