using System.Text.Json;
using SnapZones.App.Services;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class DiagnosticRunnerTests
{
    [Fact]
    public async Task Diagnostics_reports_window_placement_without_registering_a_hook()
    {
        using var directory = new TemporaryDirectory();

        var result = await DiagnosticRunner.RunForTestAsync(directory.Path, CancellationToken.None);

        Assert.True(result.WindowPlacement.Enabled);
        Assert.Equal(0, result.WindowPlacement.LearnedEntryCount);
        Assert.Equal(0, result.WindowPlacement.RuleCount);
        Assert.False(result.WindowPlacement.LifecycleHookRegistered);
    }

    [Fact]
    public async Task Diagnostics_reads_corrupt_placement_without_changing_any_file()
    {
        using var directory = new TemporaryDirectory();
        var placementPath = Path.Combine(directory.Path, "placements.json");
        await File.WriteAllTextAsync(placementPath, "{");
        var before = await File.ReadAllBytesAsync(placementPath);

        var result = await DiagnosticRunner.RunForTestAsync(directory.Path, CancellationToken.None);

        Assert.Equal("invalid-json", result.WindowPlacement.Status);
        Assert.Equal(0, result.WindowPlacement.LearnedEntryCount);
        Assert.Equal(before, await File.ReadAllBytesAsync(placementPath));
        Assert.Equal(["placements.json"], Directory.EnumerateFiles(directory.Path).Select(Path.GetFileName).Order());
    }

    [Fact]
    public async Task Diagnostics_reports_the_effective_placement_switch_and_counts()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(directory.Path, "settings.json"),
            "{\"schemaVersion\":2,\"settings\":{\"restoreWindowPlacementEnabled\":false,\"windowPlacementRules\":[{},{}]}}");
        await File.WriteAllTextAsync(
            Path.Combine(directory.Path, "placements.json"),
            "{\"entries\":[{},{}]}");

        var result = await DiagnosticRunner.RunForTestAsync(directory.Path, CancellationToken.None);

        Assert.False(result.WindowPlacement.Enabled);
        Assert.Equal(2, result.WindowPlacement.LearnedEntryCount);
        Assert.Equal(2, result.WindowPlacement.RuleCount);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("1")]
    [InlineData("\"Text\"")]
    public async Task Diagnostics_reports_a_non_object_placement_document_without_throwing(string placementContents)
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "placements.json"), placementContents);

        var result = await DiagnosticRunner.RunForTestAsync(directory.Path, CancellationToken.None);

        Assert.Equal("invalid-structure", result.WindowPlacement.Status);
        Assert.Equal(0, result.WindowPlacement.LearnedEntryCount);
        Assert.False(result.WindowPlacement.LifecycleHookRegistered);
    }

    [Fact]
    public async Task Diagnostics_reports_schema_invalid_children_without_throwing()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(directory.Path, "settings.json"),
            "{\"schemaVersion\":2,\"settings\":{\"restoreWindowPlacementEnabled\":\"false\",\"windowPlacementRules\":null}}");
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "placements.json"), "{\"entries\":null}");

        var result = await DiagnosticRunner.RunForTestAsync(directory.Path, CancellationToken.None);

        Assert.Equal("invalid-structure", result.ConfigurationStatus);
        Assert.Equal("invalid-structure", result.WindowPlacement.Status);
        Assert.True(result.WindowPlacement.Enabled);
        Assert.Equal(0, result.WindowPlacement.RuleCount);
    }

    [Fact]
    public async Task Diagnostics_result_serializes_the_window_placement_contract()
    {
        using var directory = new TemporaryDirectory();
        var result = await DiagnosticRunner.RunForTestAsync(directory.Path, CancellationToken.None);

        using var document = JsonDocument.Parse(DiagnosticRunner.Serialize(result));
        var windowPlacement = document.RootElement.GetProperty("windowPlacement");

        Assert.True(windowPlacement.GetProperty("enabled").GetBoolean());
        Assert.Equal(0, windowPlacement.GetProperty("learnedEntryCount").GetInt32());
        Assert.Equal(0, windowPlacement.GetProperty("ruleCount").GetInt32());
        Assert.False(windowPlacement.GetProperty("lifecycleHookRegistered").GetBoolean());
    }
}
