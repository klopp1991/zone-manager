using SnapZones.App.Services;
using SnapZones.Core.Geometry;
using SnapZones.Core.Persistence;
using SnapZones.Core.Placement;
using SnapZones.Core.Models;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class WindowPlacementSaveCoordinatorTests
{
    [Fact]
    public async Task Multiple_requests_inside_the_debounce_window_write_only_the_latest_catalog()
    {
        var repository = new RecordingPlacementRepository();
        var coordinator = new WindowPlacementSaveCoordinator(repository, TimeSpan.FromMilliseconds(20));

        coordinator.RequestSave(new WindowPlacementCatalog(1, []));
        coordinator.RequestSave(new WindowPlacementCatalog(1, [CreateEntry("latest")]));

        await coordinator.FlushAsync(CancellationToken.None);

        Assert.Single(repository.Saved);
        Assert.Equal("latest", repository.Saved[0].Entries[0].Identity.WindowClass);
    }

    [Fact]
    public async Task Flush_surfaces_the_last_repository_error()
    {
        var coordinator = new WindowPlacementSaveCoordinator(new ThrowingPlacementRepository(), TimeSpan.Zero);
        coordinator.RequestSave(WindowPlacementCatalog.Empty);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.FlushAsync(CancellationToken.None));

        Assert.Equal("Die Fensterplatzierungen konnten nicht gespeichert werden.", exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    private static WindowPlacementEntry CreateEntry(string windowClass) => new(
        new WindowIdentity("app", windowClass, WindowKind.MainWindow),
        "DISPLAY-A",
        null,
        new MonitorWorkArea(0, 0, 1920, 1080),
        new PixelRect(10, 20, 800, 600),
        new NormalizedRect(0, 0, 0.4, 0.5),
        false,
        DateTimeOffset.UtcNow);

    private sealed class RecordingPlacementRepository : IWindowPlacementRepository
    {
        public List<WindowPlacementCatalog> Saved { get; } = [];

        public Task<WindowPlacementLoadResult> LoadAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveAsync(WindowPlacementCatalog catalog, CancellationToken cancellationToken)
        {
            Saved.Add(catalog);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPlacementRepository : IWindowPlacementRepository
    {
        public Task<WindowPlacementLoadResult> LoadAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveAsync(WindowPlacementCatalog catalog, CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException("repository failure"));
    }
}
