using SnapZones.Presentation.Services;
using SnapZones.Core.Models;
using SnapZones.Core.Persistence;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Persistence;

public sealed class ConfigurationSaveCoordinatorTests
{
    [Fact]
    public async Task Pending_revisions_are_coalesced_and_the_latest_revision_is_written_last()
    {
        var repository = new ControlledRepository();
        var coordinator = new ConfigurationSaveCoordinator(repository);
        var first = ConfigurationSamples.TwoLayouts();
        var second = first with { Settings = first.Settings with { ZoneGap = 12 } };
        var latest = first with { Settings = first.Settings with { ZoneGap = 24 } };

        coordinator.RequestSave(first);
        await repository.FirstSaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.RequestSave(second);
        coordinator.RequestSave(latest);
        repository.ReleaseFirstSave.SetResult();
        await coordinator.FlushAsync(CancellationToken.None);

        Assert.Equal(2, repository.Saved.Count);
        Assert.Equal(first.Settings.ZoneGap, repository.Saved[0].Settings.ZoneGap);
        Assert.Equal(latest.Settings.ZoneGap, repository.Saved[1].Settings.ZoneGap);
    }

    private sealed class ControlledRepository : IConfigurationRepository
    {
        public TaskCompletionSource FirstSaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstSave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<SnapConfiguration> Saved { get; } = [];

        public Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async Task SaveAsync(SnapConfiguration configuration, CancellationToken cancellationToken)
        {
            Saved.Add(configuration);
            if (Saved.Count == 1)
            {
                FirstSaveStarted.SetResult();
                await ReleaseFirstSave.Task.WaitAsync(cancellationToken);
            }
        }
    }
}
