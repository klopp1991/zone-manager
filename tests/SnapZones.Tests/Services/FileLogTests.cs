using SnapZones.App.Services;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class FileLogTests
{
    [Fact]
    public void Debug_lines_are_dropped_unless_the_minimum_level_allows_them()
    {
        using var directory = new TemporaryDirectory();
        var log = new FileLog(directory.Path);

        log.Write("DEBUG", "Fensterereignis");
        log.Write("INFO", "Gestartet");

        var content = File.ReadAllText(log.FilePath);
        Assert.DoesNotContain("Fensterereignis", content);
        Assert.Contains("[INFO] Gestartet", content);
    }

    [Fact]
    public void Verbose_log_keeps_debug_lines()
    {
        using var directory = new TemporaryDirectory();
        var log = new FileLog(directory.Path, "DEBUG");

        log.Write("DEBUG", "Fensterereignis");

        Assert.Contains("[DEBUG] Fensterereignis", File.ReadAllText(log.FilePath));
    }

    [Fact]
    public void Exceptions_are_written_with_stack_trace_and_inner_exception()
    {
        using var directory = new TemporaryDirectory();
        var log = new FileLog(directory.Path);
        Exception thrown;
        try
        {
            throw new InvalidOperationException("Aussen", new FileNotFoundException("Innen"));
        }
        catch (Exception exception)
        {
            thrown = exception;
        }

        log.Write("FATAL", "Unbehandelter UI-Fehler.", thrown);

        var content = File.ReadAllText(log.FilePath);
        Assert.Contains("[FATAL] Unbehandelter UI-Fehler. | InvalidOperationException: Aussen", content);
        Assert.Contains("FileNotFoundException: Innen", content);
        Assert.Contains(nameof(Exceptions_are_written_with_stack_trace_and_inner_exception), content);
    }

    [Fact]
    public void Rotation_keeps_five_generations()
    {
        using var directory = new TemporaryDirectory();
        var log = new FileLog(directory.Path);
        var filler = new string('x', 2048);

        // 1 MB pro Generation, sechs Generationen erzwingen: die aelteste faellt weg.
        for (var generation = 0; generation < 6; generation++)
        {
            for (var line = 0; line < 520; line++)
            {
                log.Write("INFO", $"G{generation} {filler}");
            }
        }

        Assert.True(File.Exists(log.FilePath));
        Assert.True(File.Exists(log.FilePath + ".1"));
        Assert.True(File.Exists(log.FilePath + ".5"));
        Assert.False(File.Exists(log.FilePath + ".6"));
        Assert.Contains("G0", File.ReadAllText(log.FilePath + ".5"));
    }
}
