using System.ComponentModel;
using System.Runtime.InteropServices;
using SnapZones.Core.AppRules;
using SnapZones.Core.Drag;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Hooks;

public sealed class WindowRuleHook : IWindowRuleHook
{
    private const uint SystemForeground = 0x0003;
    private const uint ObjectShow = 0x8002;
    private const uint OutOfContext = 0x0000;
    private readonly SynchronizationContext synchronizationContext;
    private readonly HookCircuitBreaker circuitBreaker = new(500, TimeSpan.FromSeconds(10));
    private readonly User32.WinEventProc callback;
    private nint foregroundHook;
    private nint showHook;
    private bool disposed;

    public WindowRuleHook(SynchronizationContext synchronizationContext)
    {
        this.synchronizationContext = synchronizationContext ?? throw new ArgumentNullException(nameof(synchronizationContext));
        callback = OnWinEvent;
    }

    public event Action<AppRuleEvent, nint>? RuleEvent;
    public event Action<string>? EmergencyStopped;

    public bool IsEnabled => foregroundHook != 0 && showHook != 0;

    public void Enable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsEnabled)
        {
            return;
        }

        circuitBreaker.Reset();
        foregroundHook = User32.SetWinEventHook(
            SystemForeground,
            SystemForeground,
            0,
            callback,
            0,
            0,
            OutOfContext);
        showHook = User32.SetWinEventHook(ObjectShow, ObjectShow, 0, callback, 0, 0, OutOfContext);
        if (foregroundHook == 0 || showHook == 0)
        {
            var error = Marshal.GetLastWin32Error();
            Disable();
            throw new Win32Exception(error, "Der Hook für App-Regeln konnte nicht aktiviert werden.");
        }
    }

    public void Disable()
    {
        Unhook(ref foregroundHook);
        Unhook(ref showHook);
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
        _ = eventThread;
        _ = eventTime;
        try
        {
            if (window == 0 || objectId != 0 || childId != 0)
            {
                return;
            }

            if (circuitBreaker.RecordEvent(DateTimeOffset.UtcNow))
            {
                StopForSafety(circuitBreaker.Reason ?? "Der Schutzschalter der App-Regeln wurde ausgelöst.");
                return;
            }

            var mapped = eventType == SystemForeground
                ? AppRuleEvent.WindowFocused
                : AppRuleEvent.WindowCreated;
            synchronizationContext.Post(_ =>
            {
                try
                {
                    RuleEvent?.Invoke(mapped, window);
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

    private static void Unhook(ref nint field)
    {
        var handle = Interlocked.Exchange(ref field, 0);
        if (handle != 0)
        {
            _ = User32.UnhookWinEvent(handle);
        }
    }
}
