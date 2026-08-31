using ZoneManager.Windows.Native;

namespace ZoneManager.Windows.Windows;

public interface IWindowSelectionService
{
    Task<nint> SelectNextAsync(int ownProcessId, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class WindowSelectionService : IWindowSelectionService
{
    private const uint EventSystemForeground = 0x0003;
    private static readonly WindowSelectionHookCircuit ProcessHookCircuit = new();
    private readonly IPlacementWindowService windows;
    private readonly IWinEventHookApi nativeApi;
    private readonly Action<string>? diagnostic;
    private readonly WindowSelectionHookCircuit hookCircuit;

    public WindowSelectionService(IPlacementWindowService windows, Action<string>? diagnostic = null)
        : this(windows, new User32WinEventHookApi(), diagnostic, ProcessHookCircuit)
    {
    }

    internal WindowSelectionService(
        IPlacementWindowService windows,
        IWinEventHookApi nativeApi,
        Action<string>? diagnostic = null)
        : this(windows, nativeApi, diagnostic, new WindowSelectionHookCircuit())
    {
    }

    internal WindowSelectionService(
        IPlacementWindowService windows,
        IWinEventHookApi nativeApi,
        Action<string>? diagnostic,
        WindowSelectionHookCircuit hookCircuit)
    {
        this.windows = windows ?? throw new ArgumentNullException(nameof(windows));
        this.nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        this.diagnostic = diagnostic;
        this.hookCircuit = hookCircuit ?? throw new ArgumentNullException(nameof(hookCircuit));
    }

    public async Task<nint> SelectNextAsync(
        int ownProcessId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (ownProcessId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ownProcessId));
        }
        if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        if (!hookCircuit.TryReserve())
        {
            ReportCleanupFailure(hookCircuit.IsTripped
                ? "Die Fensterwahl bleibt nach einem Hook-Cleanup-Fehler deaktiviert."
                : "Eine Fensterwahl läuft bereits.");
            return 0;
        }

        var reservationCompleted = false;
        try
        {
            var completion = new TaskCompletionSource<nint>(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbackActive = 1;
            User32.WinEventProc callback = (_, eventType, window, _, _, _, _) =>
            {
                if (Volatile.Read(ref callbackActive) == 0 ||
                    eventType != EventSystemForeground ||
                    window == 0 ||
                    completion.Task.IsCompleted)
                {
                    return;
                }

                try
                {
                    if (windows.Inspect(window, ownProcessId) is not null)
                    {
                        completion.TrySetResult(window);
                    }
                }
                catch (Exception)
                {
                    // Nicht lesbare oder inzwischen geschlossene Fenster werden ignoriert.
                }
            };

            var hook = nativeApi.SetWinEventHook(
                EventSystemForeground,
                EventSystemForeground,
                0,
                callback,
                0,
                0,
                User32.WinEventOutOfContext);
            if (hook == 0)
            {
                Volatile.Write(ref callbackActive, 0);
                return 0;
            }

            try
            {
                return await completion.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            finally
            {
                Volatile.Write(ref callbackActive, 0);
                var released = false;
                string? cleanupFailure = null;
                try
                {
                    released = nativeApi.UnhookWinEvent(hook);
                }
                catch (Exception exception)
                {
                    cleanupFailure = $"Der Fensterwahl-Hook konnte nicht gelöst werden: {exception.Message}";
                }

                if (released)
                {
                    hookCircuit.Release();
                }
                else
                {
                    hookCircuit.Trip(callback);
                    ReportCleanupFailure(cleanupFailure ?? "Der Fensterwahl-Hook konnte nicht gelöst werden und bleibt deaktiviert.");
                }

                reservationCompleted = true;
                GC.KeepAlive(callback);
            }
        }
        finally
        {
            if (!reservationCompleted)
            {
                hookCircuit.Release();
            }
        }
    }

    private void ReportCleanupFailure(string message)
    {
        try
        {
            diagnostic?.Invoke(message);
        }
        catch (Exception)
        {
            // Diagnosefehler dürfen den fail-closed Cleanup nicht rückgängig machen.
        }
    }
}

internal sealed class WindowSelectionHookCircuit
{
    private const int Available = 0;
    private const int Reserved = 1;
    private const int Tripped = 2;
    private readonly object synchronization = new();
    private User32.WinEventProc? retainedCallback;
    private int state;

    public bool IsTripped => Volatile.Read(ref state) == Tripped;

    public bool TryReserve() =>
        Interlocked.CompareExchange(ref state, Reserved, Available) == Available;

    public void Release() =>
        _ = Interlocked.CompareExchange(ref state, Available, Reserved);

    public void Trip(User32.WinEventProc callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (synchronization)
        {
            if (state == Tripped)
            {
                return;
            }

            retainedCallback = callback;
            Volatile.Write(ref state, Tripped);
        }
    }

    public void Reset()
    {
        lock (synchronization)
        {
            retainedCallback = null;
            Volatile.Write(ref state, Available);
        }
    }
}
