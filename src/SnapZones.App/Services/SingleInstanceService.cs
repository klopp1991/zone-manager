namespace SnapZones.App.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle activationEvent;
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
    }

    public event Action? ActivationRequested;

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

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        cancellation.Cancel();
        activationEvent.Set();
        listenerTask?.GetAwaiter().GetResult();
        activationEvent.Dispose();
        if (ownsMutex)
        {
            mutex.ReleaseMutex();
        }

        mutex.Dispose();
        cancellation.Dispose();
    }

    private void Listen()
    {
        while (!cancellation.IsCancellationRequested)
        {
            activationEvent.WaitOne();
            if (!cancellation.IsCancellationRequested)
            {
                synchronizationContext.Post(_ => ActivationRequested?.Invoke(), null);
            }
        }
    }
}
