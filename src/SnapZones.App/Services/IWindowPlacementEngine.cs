using SnapZones.Core.Placement;

namespace SnapZones.App.Services;

public interface IWindowPlacementEngine
{
    WindowPlacementCatalog Catalog { get; }
    event Action<WindowPlacementCatalog>? CatalogChanged;

    void Start();
    void Stop();
    void EmergencyStop();
    void ReplaceCatalog(WindowPlacementCatalog catalog);
    Task ApplyNowAsync(WindowIdentity identity, CancellationToken cancellationToken);
    Task ApplyProfileRulesAsync(Guid profileId, CancellationToken cancellationToken);
    void Forget(WindowIdentity identity);
    void ForgetAll();
    void RememberExplicitZone(nint windowHandle, Guid profileId, string monitorStableId, Guid zoneId);
    Task FlushAsync(CancellationToken cancellationToken);
}
