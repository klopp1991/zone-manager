using ZoneManager.Core.Persistence;

namespace ZoneManager.App.Services;

public static class WindowPlacementStartupLoad
{
    public static Task<WindowPlacementLoadResult> Start(
        IWindowPlacementRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return Task.Run(
            async () => await repository.LoadAsync(cancellationToken).ConfigureAwait(false),
            CancellationToken.None);
    }
}
