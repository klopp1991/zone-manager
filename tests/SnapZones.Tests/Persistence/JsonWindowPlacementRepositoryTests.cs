using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Persistence;
using SnapZones.Core.Placement;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Persistence;

public sealed class JsonWindowPlacementRepositoryTests
{
    [Fact]
    public async Task Save_then_load_is_atomic_and_keeps_only_the_500_newest_entries()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonWindowPlacementRepository(directory.Path);
        var entries = Enumerable.Range(0, 501).Select(CreateEntry).ToArray();

        await repository.SaveAsync(new WindowPlacementCatalog(1, entries), CancellationToken.None);
        var loaded = await repository.LoadAsync(CancellationToken.None);

        Assert.Equal(500, loaded.Catalog.Entries.Count);
        Assert.DoesNotContain(loaded.Catalog.Entries, item => item.Identity.WindowClass == "Class-0");
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task Load_renames_corrupt_primary_and_recovers_the_last_valid_backup()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonWindowPlacementRepository(directory.Path);
        var expected = new WindowPlacementCatalog(1, [CreateEntry(1)]);
        await repository.SaveAsync(expected, CancellationToken.None);
        await repository.SaveAsync(new WindowPlacementCatalog(1, [CreateEntry(2)]), CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "placements.json"), "{");

        var loaded = await repository.LoadAsync(CancellationToken.None);

        Assert.True(loaded.RecoveredFromError);
        Assert.Equal(expected.Entries, loaded.Catalog.Entries);
        Assert.Single(Directory.GetFiles(directory.Path, "placements.invalid-*.json"));
    }

    [Fact]
    public async Task Load_recovers_a_valid_backup_when_the_primary_file_is_missing()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonWindowPlacementRepository(directory.Path);
        var expected = new WindowPlacementCatalog(1, [CreateEntry(1)]);
        await repository.SaveAsync(expected, CancellationToken.None);
        await repository.SaveAsync(new WindowPlacementCatalog(1, [CreateEntry(2)]), CancellationToken.None);
        File.Delete(Path.Combine(directory.Path, "placements.json"));

        var loaded = await repository.LoadAsync(CancellationToken.None);

        Assert.True(loaded.RecoveredFromError);
        Assert.Equal(expected.Entries, loaded.Catalog.Entries);
        Assert.True(File.Exists(Path.Combine(directory.Path, "placements.json")));
    }

    [Fact]
    public async Task Save_deduplicates_window_identities_and_keeps_the_newest_entry()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonWindowPlacementRepository(directory.Path);
        var older = CreateEntry(1);
        var newer = older with { LastUpdatedUtc = older.LastUpdatedUtc.AddMinutes(1) };

        await repository.SaveAsync(new WindowPlacementCatalog(1, [older, newer]), CancellationToken.None);
        var loaded = await repository.LoadAsync(CancellationToken.None);

        var entry = Assert.Single(loaded.Catalog.Entries);
        Assert.Equal(newer.LastUpdatedUtc, entry.LastUpdatedUtc);
    }

    [Fact]
    public async Task Load_propagates_cancellation_without_creating_an_invalid_file()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonWindowPlacementRepository(directory.Path);
        await repository.SaveAsync(new WindowPlacementCatalog(1, [CreateEntry(1)]), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.LoadAsync(cancellation.Token));

        Assert.Empty(Directory.GetFiles(directory.Path, "placements.invalid-*.json"));
    }

    private static WindowPlacementEntry CreateEntry(int index) => new(
        new WindowIdentity($"C:\\Apps\\App-{index}.exe", $"Class-{index}", WindowKind.MainWindow),
        "DISPLAY-A", null, new MonitorWorkArea(0, 0, 1920, 1080),
        new PixelRect(10, 10, 800, 600), new NormalizedRect(0, 0, 0.5, 0.5),
        false, DateTimeOffset.UnixEpoch.AddMinutes(index));
}
