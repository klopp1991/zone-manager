using SnapZones.Core.Persistence;
using SnapZones.Core.Placement;

namespace SnapZones.App.Services;

public sealed class WindowPlacementSaveCoordinator : IWindowPlacementSaveStatusSource
{
    private readonly IWindowPlacementRepository repository;
    private readonly TimeSpan debounceDelay;
    private readonly object synchronization = new();
    private WindowPlacementCatalog? pendingCatalog;
    private Task workerTask = Task.CompletedTask;
    private bool workerRunning;
    private Exception? lastException;

    public WindowPlacementSaveCoordinator(
        IWindowPlacementRepository repository,
        TimeSpan debounceDelay)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        if (debounceDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounceDelay));
        }

        this.debounceDelay = debounceDelay;
    }

    public event Action<Exception?>? SaveFinished;

    public void RequestSave(WindowPlacementCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        lock (synchronization)
        {
            pendingCatalog = catalog;
            lastException = null;
            if (!workerRunning)
            {
                workerRunning = true;
                workerTask = Task.Run(ProcessPendingAsync);
            }
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task currentWorker;
            lock (synchronization)
            {
                if (lastException is not null)
                {
                    throw new InvalidOperationException(
                        "Die Fensterplatzierungen konnten nicht gespeichert werden.",
                        lastException);
                }

                if (!workerRunning && pendingCatalog is null)
                {
                    return;
                }

                currentWorker = workerTask;
            }

            await currentWorker.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingAsync()
    {
        try
        {
            while (true)
            {
                await Task.Delay(debounceDelay).ConfigureAwait(false);

                WindowPlacementCatalog? catalog;
                lock (synchronization)
                {
                    catalog = pendingCatalog;
                    pendingCatalog = null;
                    if (catalog is null)
                    {
                        workerRunning = false;
                        return;
                    }
                }

                await repository.SaveAsync(catalog, CancellationToken.None).ConfigureAwait(false);
                SaveFinished?.Invoke(null);
            }
        }
        catch (Exception exception)
        {
            lock (synchronization)
            {
                workerRunning = false;
                lastException = exception;
            }

            SaveFinished?.Invoke(exception);
        }
    }
}
