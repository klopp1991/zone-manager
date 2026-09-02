using SnapZones.Core.AppRules;
using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.PartMonitors;
using Xunit;

namespace SnapZones.Tests.AppRules;

/// <summary>
/// Ein Ausschluss laesst ein Fenster vollstaendig in Ruhe: kein Overlay beim Ziehen, kein Einrasten,
/// keine App-Regel, kein Merken der Position. Diese Tests halten beide Enden davon fest — den Vergleich
/// selbst und die Wirkung im Ziehpfad.
/// </summary>
public sealed class AppExclusionTests
{
    [Fact]
    public void Process_file_name_matches_regardless_of_install_directory()
    {
        var exclusion = Exclusion(processPath: "notepad.exe");

        Assert.True(AppExclusionMatcher.IsExcluded(
            [exclusion],
            Window(@"C:\Windows\System32\notepad.exe", "Unbenannt", "Notepad")));
        Assert.True(AppExclusionMatcher.IsExcluded(
            [exclusion],
            Window(@"D:\Portable\notepad.exe", "Unbenannt", "Notepad")));
    }

    [Fact]
    public void Full_path_matches_only_that_exact_file()
    {
        var exclusion = Exclusion(processPath: @"C:\Apps\v1\tool.exe");

        Assert.True(AppExclusionMatcher.IsExcluded(
            [exclusion],
            Window(@"C:\Apps\v1\tool.exe", "Titel", "Klasse")));
        Assert.False(AppExclusionMatcher.IsExcluded(
            [exclusion],
            Window(@"C:\Apps\v2\tool.exe", "Titel", "Klasse")));
    }

    [Fact]
    public void Title_pattern_alone_excludes_without_naming_a_program()
    {
        var exclusion = Exclusion(processPath: string.Empty, titlePattern: "Rechner*");

        Assert.True(AppExclusionMatcher.IsExcluded(
            [exclusion],
            Window("beliebig.exe", "Rechner - Standard", "Klasse")));
        Assert.False(AppExclusionMatcher.IsExcluded(
            [exclusion],
            Window("beliebig.exe", "Editor", "Klasse")));
    }

    [Fact]
    public void Window_class_narrows_an_excluded_program()
    {
        var exclusion = Exclusion(processPath: "explorer.exe", windowClass: "CabinetWClass");

        Assert.True(AppExclusionMatcher.IsExcluded(
            [exclusion],
            Window("explorer.exe", "Downloads", "CabinetWClass")));
        Assert.False(AppExclusionMatcher.IsExcluded(
            [exclusion],
            Window("explorer.exe", "Desktop", "Progman")));
    }

    [Fact]
    public void Disabled_exclusion_never_matches()
    {
        var exclusion = Exclusion(processPath: "notepad.exe") with { IsEnabled = false };

        Assert.False(AppExclusionMatcher.IsExcluded(
            [exclusion],
            Window("notepad.exe", "Unbenannt", "Notepad")));
    }

    [Fact]
    public void Exclusion_without_any_criteria_never_matches()
    {
        // Ohne Merkmal wuerde der Ausschluss auf jedes Fenster passen und die Anwendung stillegen.
        var exclusion = Exclusion(processPath: string.Empty);

        Assert.False(exclusion.HasCriteria);
        Assert.False(AppExclusionMatcher.IsExcluded(
            [exclusion],
            Window("notepad.exe", "Unbenannt", "Notepad")));
    }

    [Fact]
    public void Missing_process_path_still_allows_title_and_class_exclusions()
    {
        // Windows verweigert den Programmpfad bei hoeher berechtigten Prozessen; Titel und Klasse
        // bleiben lesbar und muessen deshalb weiterhin greifen.
        var byTitle = Exclusion(processPath: string.Empty, titlePattern: "Taskmanager");
        var byProgram = Exclusion(processPath: "taskmgr.exe");
        var window = Window(string.Empty, "Taskmanager", "TaskManagerWindow");

        Assert.True(AppExclusionMatcher.IsExcluded([byTitle], window));
        Assert.False(AppExclusionMatcher.IsExcluded([byProgram], window));
    }

    [Fact]
    public void Excluded_window_gets_no_overlay_and_no_drag_state()
    {
        var coordinator = CreateCoordinator([Exclusion(processPath: "notepad.exe")]);
        var actions = new List<DragAction>();
        coordinator.ActionRequested += actions.Add;

        coordinator.Start((nint)42, Snapshot("notepad.exe", "Unbenannt", "Notepad"), new PointInt(100, 100));

        Assert.Empty(actions);
        Assert.Equal(DragState.Idle, coordinator.State);
    }

    [Fact]
    public void Window_outside_the_exclusion_still_snaps()
    {
        var coordinator = CreateCoordinator([Exclusion(processPath: "notepad.exe")]);
        var actions = new List<DragAction>();
        coordinator.ActionRequested += actions.Add;

        coordinator.Start((nint)42, Snapshot("code.exe", "Projekt", "Chrome_WidgetWin_1"), new PointInt(100, 100));

        Assert.IsType<ShowOverlaysAction>(Assert.Single(actions));
        Assert.Equal(DragState.Tracking, coordinator.State);
    }

    [Fact]
    public void Window_without_readable_identity_is_never_excluded()
    {
        // Ohne Identitaet laesst sich kein Ausschluss belegen; im Zweifel bleibt die Snap-Funktion aktiv.
        var coordinator = CreateCoordinator([Exclusion(processPath: "notepad.exe")]);
        var actions = new List<DragAction>();
        coordinator.ActionRequested += actions.Add;

        coordinator.Start((nint)42, new WindowSnapshot(true, false, false, false, false, true), new PointInt(100, 100));

        Assert.IsType<ShowOverlaysAction>(Assert.Single(actions));
    }

    [Fact]
    public void Display_name_prefers_title_then_file_name_and_never_the_full_path()
    {
        Assert.Equal(
            "notepad.exe",
            Exclusion(processPath: @"C:\Windows\System32\notepad.exe").DisplayName);
        Assert.Equal(
            "Rechner*",
            Exclusion(processPath: @"C:\Windows\notepad.exe", titlePattern: "Rechner*").DisplayName);
        Assert.Equal(
            "CabinetWClass",
            Exclusion(processPath: string.Empty, windowClass: "CabinetWClass").DisplayName);
    }

    private static AppExclusion Exclusion(
        string processPath,
        string? titlePattern = null,
        string? windowClass = null) =>
        new(Guid.NewGuid(), processPath, titlePattern, windowClass, true);

    private static AppWindowIdentity Window(string processPath, string title, string windowClass) =>
        new(1234, processPath, title, windowClass);

    private static WindowSnapshot Snapshot(string processPath, string title, string windowClass) =>
        new(true, false, false, false, false, true, Window(processPath, title, windowClass));

    private static WindowDragCoordinator CreateCoordinator(IReadOnlyList<AppExclusion> exclusions)
    {
        var monitor = new LiveMonitor(
            new MonitorIdentity("A", "DISPLAY1", "Links"),
            new MonitorWorkArea(0, 0, 1920, 1040),
            96,
            96,
            true);
        var targets = new[]
        {
            new PartMonitorTarget(monitor, [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)])
        };
        return new WindowDragCoordinator(
            targets,
            new LayoutMetrics(0, 0),
            OverlayScope.AllMonitors,
            exclusions);
    }
}
