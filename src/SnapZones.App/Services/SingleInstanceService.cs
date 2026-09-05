namespace SnapZones.App.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle activationEvent;
    private readonly EventWaitHandle exitEvent;
    private readonly SynchronizationContext synchronizationContext;
    private RegisteredWaitHandle? listenerRegistration;
    private RegisteredWaitHandle? exitListenerRegistration;
    private bool ownsMutex;
    private int disposed;

    public SingleInstanceService(string name, SynchronizationContext synchronizationContext)
    {
        this.synchronizationContext = synchronizationContext;
        mutex = new Mutex(initiallyOwned: true, $"Local\\{name}.Mutex", out ownsMutex);
        activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $"Local\\{name}.Activate");
        exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $"Local\\{name}.Exit");
    }

    /// <summary>Eine zweite Instanz wurde gestartet und möchte das Fenster sehen.</summary>
    public event Action? ActivationRequested;

    /// <summary>
    /// Eine zweite Instanz wurde mit <c>--exit</c> gestartet und bittet um ein geordnetes Beenden. So
    /// tauscht ein Build oder ein Skript die Programmdatei aus, ohne sie unter dem laufenden Prozess
    /// wegzuziehen.
    /// </summary>
    public event Action? ExitRequested;

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
        exitListenerRegistration ??= ThreadPool.RegisterWaitForSingleObject(
            exitEvent,
            static (state, _) => ((SingleInstanceService)state!).HandleExit(),
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

    public void NotifyPrimaryExit()
    {
        if (!ownsMutex)
        {
            exitEvent.Set();
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
        exitListenerRegistration?.Unregister(null);
        exitListenerRegistration = null;
        activationEvent.Dispose();
        exitEvent.Dispose();
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
            static state => ((SingleInstanceService)state!).Raise(((SingleInstanceService)state!).ActivationRequested),
            this);
    }

    private void HandleExit()
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            return;
        }

        synchronizationContext.Post(
            static state => ((SingleInstanceService)state!).Raise(((SingleInstanceService)state!).ExitRequested),
            this);
    }

    private void Raise(Action? handler)
    {
        if (Volatile.Read(ref disposed) == 0)
        {
            handler?.Invoke();
        }
    }
}
