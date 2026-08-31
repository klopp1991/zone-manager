using ZoneManager.App.Services;
using ZoneManager.Core.Geometry;
using ZoneManager.Core.Persistence;
using ZoneManager.Core.Placement;
using ZoneManager.Core.Models;
using Xunit;

namespace ZoneManager.Tests.Services;

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

    [Fact]
    public async Task Save_finished_with_zero_debounce_runs_outside_the_coordinator_lock()
    {
        var repository = new RecordingPlacementRepository();
        var coordinator = new WindowPlacementSaveCoordinator(repository, TimeSpan.Zero);
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reentrantAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reentrantCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackCount = 0;
        var reentrantWasBlocked = false;
        coordinator.SaveFinished += _ =>
        {
            if (Interlocked.Increment(ref callbackCount) != 1)
            {
                return;
            }

            callbackEntered.TrySetResult();
            var thread = new Thread(() =>
            {
                reentrantAttempted.TrySetResult();
                coordinator.RequestSave(new WindowPlacementCatalog(1, [CreateEntry("reentrant")]));
                reentrantCompleted.TrySetResult();
            });
            thread.IsBackground = true;
            thread.Start();
            if (!reentrantCompleted.Task.Wait(TimeSpan.FromMilliseconds(500)))
            {
                reentrantWasBlocked = true;
            }
        };

        var request = Task.Run(() => coordinator.RequestSave(new WindowPlacementCatalog(1, [CreateEntry("initial")])));
        await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await reentrantAttempted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await request.WaitAsync(TimeSpan.FromSeconds(2));
        await reentrantCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await coordinator.FlushAsync(CancellationToken.None);

        Assert.False(reentrantWasBlocked);
        Assert.Equal("reentrant", repository.Saved[^1].Entries[0].Identity.WindowClass);
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

