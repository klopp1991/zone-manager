namespace SnapZones.App.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle activationEvent;
    private readonly EventWaitHandle restartEvent;
    private readonly CancellationTokenSource cancellation = new();
    private readonly SynchronizationContext synchronizationContext;
    private Task? listenerTask;
    private bool ownsMutex;
    private bool disposed;

    public SingleInstanceService(string name, SynchronizationContext synchronizationContext)
    {
        this.synchronizationContext = synchronizationContext;
        mutex = new Mutex(initiallyOwned: true, $"Local\\{name}.Mutex", out ownsMutex);
        activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $"Local\\{name}.Activate");
        restartEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $"Local\\{name}.Restart");
    }

    public event Action? ActivationRequested;
    public event Action? RestartRequested;

    public bool IsPrimary => ownsMutex;

    public void StartListening()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!ownsMutex)
        {
            throw new InvalidOperationException("Nur die primäre Instanz darf Startanforderungen empfangen.");
        }

        listenerTask ??= Task.Factory.StartNew(
            Listen,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public void NotifyPrimary()
    {
        if (!ownsMutex)
        {
            activationEvent.Set();
        }
    }

    public bool RequestRestartAndTakeOwnership(TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (ownsMutex)
        {
            return true;
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        restartEvent.Set();
        try
        {
            ownsMutex = mutex.WaitOne(timeout);
        }
        catch (AbandonedMutexException)
        {
            ownsMutex = true;
        }

        if (ownsMutex)
        {
            activationEvent.Reset();
            restartEvent.Reset();
        }

        return ownsMutex;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        cancellation.Cancel();
        activationEvent.Set();
        restartEvent.Set();
        listenerTask?.GetAwaiter().GetResult();
        activationEvent.Dispose();
        restartEvent.Dispose();
        if (ownsMutex)
        {
            mutex.ReleaseMutex();
        }

        mutex.Dispose();
        cancellation.Dispose();
    }

    private void Listen()
    {
        WaitHandle[] requests = [activationEvent, restartEvent];
        while (!cancellation.IsCancellationRequested)
        {
            var request = WaitHandle.WaitAny(requests);
            if (!cancellation.IsCancellationRequested)
            {
                var handler = request == 0 ? ActivationRequested : RestartRequested;
                synchronizationContext.Post(_ => handler?.Invoke(), null);
            }
        }
    }
}
