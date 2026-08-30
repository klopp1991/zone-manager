using SnapZones.App.Services;
using SnapZones.Core.Models;
using SnapZones.Core.Persistence;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class ExitSaveCoordinatorTests
{
    [Fact]
    public async Task PrepareForShutdown_waits_until_the_configuration_is_persisted()
    {
        var repository = new ControlledRepository();
        var saveCoordinator = new ConfigurationSaveCoordinator(repository);
        var exitSaveCoordinator = new ExitSaveCoordinator(saveCoordinator);
        var configuration = ConfigurationSamples.TwoLayouts();

        var preparation = exitSaveCoordinator.PrepareForShutdownAsync(
            () => saveCoordinator.RequestSave(configuration));
        await repository.SaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(preparation.IsCompleted);

        repository.ReleaseSave.SetResult();
        await preparation;
        Assert.Single(repository.Saved);
    }

    private sealed class ControlledRepository : IConfigurationRepository
    {
        public TaskCompletionSource SaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseSave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<SnapConfiguration> Saved { get; } = [];

        public Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async Task SaveAsync(SnapConfiguration configuration, CancellationToken cancellationToken)
        {
            Saved.Add(configuration);
            SaveStarted.SetResult();
            await ReleaseSave.Task.WaitAsync(cancellationToken);
        }
    }
}
