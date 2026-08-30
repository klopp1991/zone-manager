using SnapZones.Core.Models;
using SnapZones.Core.Persistence;

namespace SnapZones.Presentation.Services;

public sealed class ConfigurationSaveCoordinator
{
    private readonly IConfigurationRepository repository;
    private readonly object synchronization = new();
    private SnapConfiguration? pendingConfiguration;
    private Task workerTask = Task.CompletedTask;
    private bool workerRunning;
    private Exception? lastException;

    public ConfigurationSaveCoordinator(IConfigurationRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public event Action<Exception?>? SaveFinished;

    public void RequestSave(SnapConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        lock (synchronization)
        {
            pendingConfiguration = configuration;
            lastException = null;
            if (!workerRunning)
            {
                workerRunning = true;
                workerTask = ProcessPendingAsync();
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
                    throw new InvalidOperationException("Die Konfiguration konnte nicht gespeichert werden.", lastException);
                }

                if (!workerRunning && pendingConfiguration is null)
                {
                    return;
                }

                currentWorker = workerTask;
            }

            await currentWorker.WaitAsync(cancellationToken);
        }
    }

    private async Task ProcessPendingAsync()
    {
        try
        {
            while (true)
            {
                SnapConfiguration? configuration;
                lock (synchronization)
                {
                    configuration = pendingConfiguration;
                    pendingConfiguration = null;
                    if (configuration is null)
                    {
                        workerRunning = false;
                        return;
                    }
                }

                await repository.SaveAsync(configuration, CancellationToken.None);
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
