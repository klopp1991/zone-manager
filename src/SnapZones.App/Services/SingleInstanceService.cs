namespace SnapZones.App.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle activationEvent;
    private readonly SynchronizationContext synchronizationContext;
    private RegisteredWaitHandle? listenerRegistration;
    private bool ownsMutex;
    private int disposed;

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
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (!ownsMutex)
        {
            throw new InvalidOperationException("Nur die primäre Instanz darf Startanforderungen empfangen.");
        }

        listenerRegistration ??= ThreadPool.RegisterWaitForSingleObject(
            activationEvent,
            static (state, _) => ((SingleInstanceService)state!).HandleActivation(),
            this,
            Timeout.Infinite,
            executeOnlyOnce: false);
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
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        listenerRegistration?.Unregister(null);
        listenerRegistration = null;
        activationEvent.Dispose();
        if (ownsMutex)
        {
            mutex.ReleaseMutex();
        }

        mutex.Dispose();
    }

    private void HandleActivation()
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            return;
        }

        synchronizationContext.Post(
            static state => ((SingleInstanceService)state!).RaiseActivationRequested(),
            this);
    }

    private void RaiseActivationRequested()
    {
        if (Volatile.Read(ref disposed) == 0)
        {
            ActivationRequested?.Invoke();
        }
    }
}
