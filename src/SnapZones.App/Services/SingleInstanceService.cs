namespace SnapZones.App.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle activationEvent;
    private readonly CancellationTokenSource cancellation = new();
    private readonly SynchronizationContext synchronizationContext;
    private readonly bool ownsMutex;

    public SingleInstanceService(string name, SynchronizationContext synchronizationContext)
    {
        this.synchronizationContext = synchronizationContext;
        mutex = new Mutex(initiallyOwned: true, $"Local\\{name}.Mutex", out ownsMutex);
        activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $"Local\\{name}.Activate");
        if (ownsMutex)
        {
            _ = Task.Run(ListenAsync);
        }
    }

    public event Action? ActivationRequested;

    public bool IsPrimary => ownsMutex;

    public void NotifyPrimary()
    {
        if (!ownsMutex)
        {
            activationEvent.Set();
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        activationEvent.Set();
        activationEvent.Dispose();
        if (ownsMutex)
        {
            mutex.ReleaseMutex();
        }

        mutex.Dispose();
        cancellation.Dispose();
    }

    private void ListenAsync()
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
