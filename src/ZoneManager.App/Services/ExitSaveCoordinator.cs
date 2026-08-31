namespace ZoneManager.App.Services;

public sealed class ExitSaveCoordinator
{
    private readonly ConfigurationSaveCoordinator saveCoordinator;

    public ExitSaveCoordinator(ConfigurationSaveCoordinator saveCoordinator)
    {
        this.saveCoordinator = saveCoordinator ?? throw new ArgumentNullException(nameof(saveCoordinator));
    }

    public Task PrepareForShutdownAsync(Action saveConfiguration)
    {
        ArgumentNullException.ThrowIfNull(saveConfiguration);
        saveConfiguration();
        return saveCoordinator.FlushAsync(CancellationToken.None);
    }
}
