using SnapZones.App.Services;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Services;

/// <summary>
/// Der Wächter über die eigene Programmdatei. Eine Single-File-Anwendung lädt Bausteine über den Pfad
/// ihrer Programmdatei nach; wird die Datei ausgetauscht, muss das Programm es erfahren, bevor das
/// nächste Nachladen scheitert.
/// </summary>
public sealed class ExecutableIntegrityWatchTests
{
    [Fact]
    public void An_untouched_file_is_reported_as_unchanged()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "ZoneManager.exe");
        File.WriteAllText(path, "alt");
        var identity = ExecutableIdentity.TryCapture(path);

        Assert.NotNull(identity);
        Assert.Equal(ExecutableChange.Unchanged, identity.Value.Compare(path));
    }

    [Fact]
    public void A_renamed_file_is_missing_and_a_swapped_file_is_replaced()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "ZoneManager.exe");
        File.WriteAllText(path, "alt");
        var identity = ExecutableIdentity.TryCapture(path)!.Value;

        File.Move(path, path + ".previous");
        Assert.Equal(ExecutableChange.Missing, identity.Compare(path));

        // Ein frischer Build ist anders gross und anders alt.
        File.WriteAllText(path, "neu und länger");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        Assert.Equal(ExecutableChange.Replaced, identity.Compare(path));
    }

    [Fact]
    public void A_copied_file_with_identical_timestamps_still_counts_as_replaced()
    {
        // NTFS gibt der neuen Datei gleichen Namens die Erstellzeit der weggeschobenen, und ein Kopieren
        // uebernimmt die Aenderungszeit: an den Zeitstempeln ist der Austausch nicht zu erkennen, an der
        // Dateikennung schon.
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "ZoneManager.exe");
        File.WriteAllText(path, "gleich gross");
        var identity = ExecutableIdentity.TryCapture(path)!.Value;
        Assert.NotNull(identity.File);

        File.Move(path, path + ".previous");
        File.WriteAllText(path, "gleich gross");
        File.SetCreationTimeUtc(path, identity.CreationTimeUtc);
        File.SetLastWriteTimeUtc(path, identity.LastWriteTimeUtc);

        Assert.Equal(ExecutableChange.Replaced, identity.Compare(path));
    }

    [Fact]
    public void The_file_identity_survives_a_rename()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "ZoneManager.exe");
        File.WriteAllText(path, "alt");
        var before = SnapZones.Windows.Files.FileIdentity.TryRead(path);

        File.Move(path, path + ".previous");
        var after = SnapZones.Windows.Files.FileIdentity.TryRead(path + ".previous");

        Assert.NotNull(before);
        Assert.Equal(before, after);
        Assert.Null(SnapZones.Windows.Files.FileIdentity.TryRead(path));
    }

    [Fact]
    public void A_missing_file_at_startup_cannot_be_watched()
    {
        using var directory = new TemporaryDirectory();

        Assert.Null(ExecutableIdentity.TryCapture(Path.Combine(directory.Path, "fehlt.exe")));
    }

    [Fact]
    public void A_change_is_only_confirmed_by_two_consecutive_observations()
    {
        // Auf einem Netzlaufwerk fehlt eine Datei auch einmal fuer einen Augenblick, und ein Austausch
        // besteht aus zwei Schritten. Erst die Wiederholung zaehlt.
        var detector = new ExecutableChangeDetector();

        Assert.Equal(ExecutableChange.Unchanged, detector.Observe(ExecutableChange.Missing));
        Assert.Equal(ExecutableChange.Unchanged, detector.Observe(ExecutableChange.Unchanged));
        Assert.Equal(ExecutableChange.Unchanged, detector.Observe(ExecutableChange.Missing));
        Assert.Equal(ExecutableChange.Missing, detector.Observe(ExecutableChange.Missing));
    }

    [Fact]
    public void A_swap_that_ends_with_a_new_file_is_reported_as_replaced()
    {
        var detector = new ExecutableChangeDetector();

        Assert.Equal(ExecutableChange.Unchanged, detector.Observe(ExecutableChange.Missing));
        Assert.Equal(ExecutableChange.Unchanged, detector.Observe(ExecutableChange.Replaced));
        Assert.Equal(ExecutableChange.Replaced, detector.Observe(ExecutableChange.Replaced));
    }

    [Fact]
    public void An_unreadable_observation_neither_confirms_nor_clears_a_suspicion()
    {
        var detector = new ExecutableChangeDetector();

        Assert.Equal(ExecutableChange.Unchanged, detector.Observe(ExecutableChange.Replaced));
        Assert.Equal(ExecutableChange.Unchanged, detector.Observe(ExecutableChange.Unreadable));
        Assert.Equal(ExecutableChange.Replaced, detector.Observe(ExecutableChange.Replaced));
    }

    [Fact]
    public async Task The_watch_reports_a_confirmed_replacement_exactly_once()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "ZoneManager.exe");
        File.WriteAllText(path, "alt");
        var reported = new TaskCompletionSource<ExecutableChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var watch = new ExecutableIntegrityWatch(
            path,
            TimeSpan.FromMilliseconds(50),
            change =>
            {
                Interlocked.Increment(ref calls);
                reported.TrySetResult(change);
            });
        Assert.True(watch.IsArmed);

        // Wie ein Build: die alte Datei weg, eine neue an ihren Platz.
        File.Move(path, path + ".previous");
        File.WriteAllText(path, "neu und länger");

        var change = await reported.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(200);

        Assert.Equal(ExecutableChange.Replaced, change);
        Assert.Equal(1, Volatile.Read(ref calls));
    }
}
