using SnapZones.App.Services;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Persistence;
using SnapZones.Core.Placement;
using SnapZones.Windows.Hooks;
using SnapZones.Windows.Windows;

namespace SnapZones.Tests.Support;

/// <summary>
/// Fährt <see cref="WindowPlacementEngine"/> ohne Windows hoch. Hook, Fensterdienst und Ablage sind
/// Schnittstellen, Verzögerung und Zeitgeber sind injizierbar; <see cref="FlushAsync"/> der Engine wartet
/// auf alle laufenden Vorgänge und ist damit der deterministische Synchronisationspunkt.
/// </summary>
internal sealed class PlacementEngineHarness : IDisposable
{
    public const int OwnProcessId = 4242;

    private readonly FakeLifecycleHook hook = new();
    private readonly WindowPlacementSaveCoordinator saveCoordinator;

    public PlacementEngineHarness(
        SnapConfiguration configuration,
        IReadOnlyList<PlacementZoneTarget> zones,
        WindowPlacementCatalog? catalog = null,
        MonitorWorkArea? workArea = null)
    {
        Configuration = configuration;
        Zones = zones;
        var area = workArea ?? new MonitorWorkArea(0, 0, 1920, 1080);
        Monitors = [new PlacementMonitorTarget("DISPLAY-A", area, IsPrimary: true)];
        saveCoordinator = new WindowPlacementSaveCoordinator(Repository, TimeSpan.Zero);
        Engine = new WindowPlacementEngine(
            hook,
            WindowService,
            saveCoordinator,
            catalog ?? WindowPlacementCatalog.Empty,
            () => new PlacementEnvironment(Configuration, Monitors, Zones),
            OwnProcessId,
            message => Log.Add(message),
            // Die Wartezeiten der Engine sind fuer den Ablauf ohne Belang; ohne diese Abkuerzung wuerde
            // jeder Test die echten Inspektionsverzoegerungen abwarten. Ein Abbruch wird weitergereicht,
            // sonst liefe eine abgebrochene Operation im Test weiter als im Betrieb.
            delay: (_, token) => token.IsCancellationRequested
                ? Task.FromCanceled(token)
                : Task.CompletedTask);
    }

    public WindowPlacementEngine Engine { get; }
    public FakePlacementWindowService WindowService { get; } = new();
    public FakeWindowPlacementRepository Repository { get; } = new();
    public List<string> Log { get; } = [];
    public SnapConfiguration Configuration { get; set; }
    public IReadOnlyList<PlacementZoneTarget> Zones { get; set; }
    public IReadOnlyList<PlacementMonitorTarget> Monitors { get; }

    /// <summary>Meldet ein neu erschienenes Fenster und wartet, bis die Engine damit fertig ist.</summary>
    public async Task ShowWindowAsync(nint windowHandle)
    {
        hook.Raise(new WindowLifecycleEvent(windowHandle, WindowLifecycleEventKind.Shown));
        await Engine.FlushAsync(CancellationToken.None);
    }

    /// <summary>Meldet einen Fokuswechsel und wartet, bis die Engine damit fertig ist.</summary>
    public async Task FocusWindowAsync(nint windowHandle)
    {
        hook.Raise(new WindowLifecycleEvent(windowHandle, WindowLifecycleEventKind.Focused));
        await Engine.FlushAsync(CancellationToken.None);
    }

    /// <summary>Meldet ein beendetes Verschieben und wartet, bis die Engine damit fertig ist.</summary>
    public async Task EndMoveAsync(nint windowHandle)
    {
        hook.Raise(new WindowLifecycleEvent(windowHandle, WindowLifecycleEventKind.MoveSizeEnded));
        await Engine.FlushAsync(CancellationToken.None);
    }

    public void Dispose()
    {
        Engine.Stop();
        hook.Dispose();
    }

    private sealed class FakeLifecycleHook : IWindowLifecycleHook
    {
        public event Action<WindowLifecycleEvent>? EventReceived;

        // Der Notaus wird von diesen Tests nicht ausgeloest; das Ereignis gehoert zur Schnittstelle.
        public event Action<string>? EmergencyStopped
        {
            add { }
            remove { }
        }

        public bool IsEnabled { get; private set; }

        public void Enable() => IsEnabled = true;

        public void Disable() => IsEnabled = false;

        public void Raise(WindowLifecycleEvent lifecycleEvent) => EventReceived?.Invoke(lifecycleEvent);

        public void Dispose() => IsEnabled = false;
    }
}

/// <summary>Ein Satz gestellter Fenster; merkt sich jede Platzierung samt Reihenfolge.</summary>
internal sealed class FakePlacementWindowService : IPlacementWindowService
{
    private readonly Dictionary<nint, PlacementWindowSnapshot> windows = [];

    public List<(nint WindowHandle, PixelRect Bounds, bool Maximize)> Placements { get; } = [];

    public bool PlacementSucceeds { get; set; } = true;

    public void Add(PlacementWindowSnapshot snapshot) => windows[snapshot.WindowHandle] = snapshot;

    public void Remove(nint windowHandle) => windows.Remove(windowHandle);

    public PlacementWindowSnapshot? Inspect(nint windowHandle, int excludedProcessId) =>
        windows.TryGetValue(windowHandle, out var snapshot) ? snapshot : null;

    public bool TryPlace(nint windowHandle, PixelRect normalBounds, bool maximize)
    {
        Placements.Add((windowHandle, normalBounds, maximize));
        if (!PlacementSucceeds)
        {
            return false;
        }

        if (windows.TryGetValue(windowHandle, out var snapshot))
        {
            windows[windowHandle] = snapshot with
            {
                CurrentBounds = normalBounds,
                NormalBounds = normalBounds,
                IsMaximized = maximize
            };
        }

        return true;
    }

    public IReadOnlyList<nint> EnumerateEligibleWindows(int excludedProcessId) => [.. windows.Keys];

    public nint GetForegroundWindow() => windows.Keys.FirstOrDefault();
}

/// <summary>Nimmt gespeicherte Kataloge entgegen, ohne eine Datei anzufassen.</summary>
internal sealed class FakeWindowPlacementRepository : IWindowPlacementRepository
{
    public List<WindowPlacementCatalog> Saved { get; } = [];

    public WindowPlacementCatalog? Latest => Saved.Count == 0 ? null : Saved[^1];

    public Task<WindowPlacementLoadResult> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new WindowPlacementLoadResult(WindowPlacementCatalog.Empty, false));

    public Task SaveAsync(WindowPlacementCatalog catalog, CancellationToken cancellationToken)
    {
        Saved.Add(catalog);
        return Task.CompletedTask;
    }
}
