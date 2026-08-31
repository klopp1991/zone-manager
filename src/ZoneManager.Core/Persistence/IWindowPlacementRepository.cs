using ZoneManager.Core.Placement;

namespace ZoneManager.Core.Persistence;

public interface IWindowPlacementRepository
{
    Task<WindowPlacementLoadResult> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(WindowPlacementCatalog catalog, CancellationToken cancellationToken);
}
