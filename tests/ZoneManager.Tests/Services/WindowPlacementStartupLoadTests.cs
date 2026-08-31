using ZoneManager.App.Services;
using ZoneManager.Core.Persistence;
using ZoneManager.Core.Placement;
using Xunit;

namespace ZoneManager.Tests.Services;

public sealed class WindowPlacementStartupLoadTests
{
    [Fact]
    public async Task Start_enters_repository_on_a_worker_before_the_caller_releases_its_ui_wait()
    {
        var repository = new GatedRepository();
        var callerThread = Environment.CurrentManagedThreadId;
        using var cancellation = new CancellationTokenSource();

        var load = WindowPlacementStartupLoad.Start(repository, cancellation.Token);
        await repository.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.NotEqual(callerThread, repository.LoadThreadId);
        Assert.False(load.IsCompleted);
        repository.Result.TrySetResult(new(WindowPlacementCatalog.Empty, false));
        Assert.Same(WindowPlacementCatalog.Empty, (await load).Catalog);
    }

    [Fact]
    public async Task Start_propagates_cancellation_to_the_blocked_repository_load()
    {
        var repository = new GatedRepository();
        using var cancellation = new CancellationTokenSource();
        var load = WindowPlacementStartupLoad.Start(repository, cancellation.Token);
        await repository.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
        Assert.True(repository.ObservedCancellation);
    }

    [Fact]
    public async Task Start_propagates_repository_exceptions()
    {
        var repository = new GatedRepository();
        var load = WindowPlacementStartupLoad.Start(repository, CancellationToken.None);
        await repository.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        repository.Result.TrySetException(new InvalidDataException("Defekt"));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => load);
        Assert.Equal("Defekt", exception.Message);
    }

    private sealed class GatedRepository : IWindowPlacementRepository
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<WindowPlacementLoadResult> Result { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int LoadThreadId { get; private set; }
        public bool ObservedCancellation { get; private set; }

        public async Task<WindowPlacementLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            LoadThreadId = Environment.CurrentManagedThreadId;
            Started.TrySetResult();
            try
            {
                return await Result.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ObservedCancellation = true;
                throw;
            }
        }

        public Task SaveAsync(WindowPlacementCatalog catalog, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

