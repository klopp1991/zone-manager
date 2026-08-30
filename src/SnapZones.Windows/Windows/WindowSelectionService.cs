using SnapZones.Windows.Native;

namespace SnapZones.Windows.Windows;

public interface IWindowSelectionService
{
    Task<nint> SelectNextAsync(int ownProcessId, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class WindowSelectionService : IWindowSelectionService
{
    private const uint EventSystemForeground = 0x0003;
    private readonly IPlacementWindowService windows;
    private readonly IWinEventHookApi nativeApi;

    public WindowSelectionService(IPlacementWindowService windows)
        : this(windows, new User32WinEventHookApi())
    {
    }

    internal WindowSelectionService(IPlacementWindowService windows, IWinEventHookApi nativeApi)
    {
        this.windows = windows ?? throw new ArgumentNullException(nameof(windows));
        this.nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
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

        var completion = new TaskCompletionSource<nint>(TaskCreationOptions.RunContinuationsAsynchronously);
        User32.WinEventProc callback = (_, eventType, window, _, _, _, _) =>
        {
            if (eventType != EventSystemForeground || window == 0 || completion.Task.IsCompleted)
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
            _ = nativeApi.UnhookWinEvent(hook);
            GC.KeepAlive(callback);
        }
    }
}
