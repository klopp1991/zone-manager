using SnapZones.Core.Placement;

namespace SnapZones.Core.Persistence;

public interface IWindowPlacementRepository
{
    Task<WindowPlacementLoadResult> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(WindowPlacementCatalog catalog, CancellationToken cancellationToken);
}
