using SnapZones.Core.Models;

namespace SnapZones.Core.Persistence;

public interface IConfigurationRepository
{
    Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(SnapConfiguration configuration, CancellationToken cancellationToken);
}
