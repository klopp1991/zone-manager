using SnapZones.App.Services;
using SnapZones.Core.Models;
using SnapZones.Core.Persistence;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class ShutdownSaveTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(300);

    [Fact]
    public async Task Shutdown_reports_saved_when_the_configuration_is_persisted()
    {
        var repository = new RecordingRepository();
        var saveCoordinator = new ConfigurationSaveCoordinator(repository);
        var exitSaveCoordinator = new ExitSaveCoordinator(saveCoordinator);
        var configuration = ConfigurationSamples.TwoLayouts();

        var result = await exitSaveCoordinator.TryPrepareForShutdownAsync(
            () => saveCoordinator.RequestSave(configuration),
            TimeSpan.FromSeconds(5));

        Assert.True(result.IsSaved);
        Assert.Equal(ShutdownSaveOutcome.Saved, result.Outcome);
        Assert.Single(repository.Saved);
    }

    [Fact]
    public async Task Shutdown_gives_up_instead_of_waiting_forever_on_a_save_that_never_finishes()
    {
        // Genau dieser Fall liess die Anwendung frueher haengen: der Flush kam nie zur Ruhe und
        // das Beenden wartete ohne Zeitgrenze darauf.
        var repository = new BlockingRepository();
        var saveCoordinator = new ConfigurationSaveCoordinator(repository);
        var exitSaveCoordinator = new ExitSaveCoordinator(saveCoordinator);
        var configuration = ConfigurationSamples.TwoLayouts();

        var result = await exitSaveCoordinator
            .TryPrepareForShutdownAsync(() => saveCoordinator.RequestSave(configuration), ShortTimeout)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(ShutdownSaveOutcome.TimedOut, result.Outcome);
        Assert.False(result.IsSaved);
        Assert.Equal(ShortTimeout, result.Timeout);
        Assert.Contains("Sekunden", result.Describe(), StringComparison.Ordinal);
        repository.Release.SetResult();
    }

    [Fact]
    public async Task Shutdown_reports_a_failing_save_without_throwing()
    {
        var saveCoordinator = new ConfigurationSaveCoordinator(new FailingRepository());
        var exitSaveCoordinator = new ExitSaveCoordinator(saveCoordinator);
        var configuration = ConfigurationSamples.TwoLayouts();

        var result = await exitSaveCoordinator.TryPrepareForShutdownAsync(
            () => saveCoordinator.RequestSave(configuration),
            TimeSpan.FromSeconds(5));

        Assert.Equal(ShutdownSaveOutcome.Failed, result.Outcome);
        Assert.NotNull(result.Failure);
        Assert.Contains("nicht gespeichert werden", result.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shutdown_also_bounds_the_additional_flush()
    {
        var saveCoordinator = new ConfigurationSaveCoordinator(new RecordingRepository());
        var exitSaveCoordinator = new ExitSaveCoordinator(saveCoordinator);
        var configuration = ConfigurationSamples.TwoLayouts();

        var result = await exitSaveCoordinator
            .TryPrepareForShutdownAsync(
                () => saveCoordinator.RequestSave(configuration),
                ShortTimeout,
                token => Task.Delay(Timeout.Infinite, token))
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(ShutdownSaveOutcome.TimedOut, result.Outcome);
    }

    [Fact]
    public async Task Shutdown_rejects_a_timeout_that_cannot_elapse()
    {
        var saveCoordinator = new ConfigurationSaveCoordinator(new RecordingRepository());
        var exitSaveCoordinator = new ExitSaveCoordinator(saveCoordinator);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => exitSaveCoordinator.TryPrepareForShutdownAsync(() => { }, TimeSpan.Zero));
    }

    private sealed class RecordingRepository : IConfigurationRepository
    {
        public List<SnapConfiguration> Saved { get; } = [];

        public Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveAsync(SnapConfiguration configuration, CancellationToken cancellationToken)
        {
            Saved.Add(configuration);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingRepository : IConfigurationRepository
    {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveAsync(SnapConfiguration configuration, CancellationToken cancellationToken) => Release.Task;
    }

    private sealed class FailingRepository : IConfigurationRepository
    {
        public Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveAsync(SnapConfiguration configuration, CancellationToken cancellationToken) =>
            Task.FromException(new IOException("Der Zugriff auf settings.json wurde verweigert."));
    }
}
