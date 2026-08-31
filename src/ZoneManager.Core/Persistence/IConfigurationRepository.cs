using ZoneManager.Core.Models;

namespace ZoneManager.Core.Persistence;

public interface IConfigurationRepository
{
    Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(SnapConfiguration configuration, CancellationToken cancellationToken);
}
