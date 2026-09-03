using SnapZones.Core.AppRules;
using SnapZones.Core.Fullscreen;
using SnapZones.Core.Geometry;
using SnapZones.Core.Layouts;
using SnapZones.Core.PartMonitors;
using SnapZones.Core.Placement;
using SnapZones.Windows.Hooks;
using SnapZones.Windows.Windows;

namespace SnapZones.App.Services;

/// <summary>
/// Haelt das Vollbild eines eingerasteten Fensters in seiner Zone.
///
/// <para>
/// Ein Videoplayer schaltet im Browser nicht in den Exklusivmodus der Grafikkarte, sondern setzt sein
/// Fenster randlos auf die volle Monitorflaeche. Der Koordinator erkennt das an den Fensterereignissen,
/// die ohnehin fuer das Positionsgedaechtnis laufen, und setzt das Fenster auf die Zone zurueck, in der
/// es vorher lag. Der Player bleibt dabei in seinem Vollbildzustand — er rechnet nur mit einer kleineren
/// Flaeche und legt seine Bedienelemente entsprechend aus.
/// </para>
///
/// <para>
/// Angefasst wird ausschliesslich ein Fenster, das vor dem Vollbild in einer Zone lag. Was frei auf dem
/// Bildschirm liegt, geht weiterhin auf den ganzen Monitor, und ein Ausschluss gilt hier wie ueberall.
/// </para>
/// </summary>
public sealed class ZoneFullscreenCoordinator
{
    /// <summary>Nach dieser Ruhezeit ohne Korrektur beginnt die Zaehlung je Fenster von vorn.</summary>
    private static readonly TimeSpan CorrectionWindow = TimeSpan.FromSeconds(5);

    private readonly object gate = new();
    private readonly ZoneFullscreenTracker tracker = new();
    private readonly Dictionary<nint, string> lastReasons = [];
    private readonly IFullscreenWindowReader reader;
    private readonly Func<nint, PixelRect, PlacementOutcome> fill;
    private readonly Func<nint, AppWindowIdentity?> identityReader;
    private readonly Func<PlacementEnvironment> environmentFactory;
    private readonly Action<string> log;
    private readonly TimeProvider timeProvider;

    public ZoneFullscreenCoordinator(
        IFullscreenWindowReader reader,
        Func<nint, PixelRect, PlacementOutcome> fill,
        Func<nint, AppWindowIdentity?> identityReader,
        Func<PlacementEnvironment> environmentFactory,
        Action<string> log,
        TimeProvider? timeProvider = null)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.fill = fill ?? throw new ArgumentNullException(nameof(fill));
        this.identityReader = identityReader ?? throw new ArgumentNullException(nameof(identityReader));
        this.environmentFactory = environmentFactory ?? throw new ArgumentNullException(nameof(environmentFactory));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Vergisst alle beobachteten Fenster. Noetig, wenn sich die Zonen aendern: der Koordinator merkt
    /// sich Flaechen in Bildschirmkoordinaten, und nach einem Layout- oder Monitorwechsel zeigen die auf
    /// Stellen, die es so nicht mehr gibt.
    /// </summary>
    public void Reset()
    {
        lock (gate)
        {
            tracker.Clear();
            lastReasons.Clear();
        }
    }

    /// <summary>Ob gerade mindestens ein Fenster in seiner Zone gehalten wird.</summary>
    public bool HasHeldWindows
    {
        get
        {
            lock (gate)
            {
                return tracker.HeldWindows.Count > 0;
            }
        }
    }

    /// <summary>
    /// Prueft alle gehaltenen Fenster nach, unabhaengig von Ereignissen. Chromium setzt ein Vollbildfenster
    /// beim Aktivierungswechsel wieder auf die Monitorgroesse, ohne dass dafuer ein Lageereignis ankommt;
    /// wer nur auf Ereignisse wartet, sieht diesen Rueckfall nie. Laeuft auf demselben Faden wie der Hook.
    /// </summary>
    public void Poll()
    {
        IReadOnlyList<nint> held;
        lock (gate)
        {
            held = tracker.HeldWindows;
        }

        foreach (var window in held)
        {
            Evaluate(window);
        }
    }

    /// <summary>Nimmt ein Fensterereignis entgegen. Laeuft auf demselben Faden wie der Hook.</summary>
    public void Handle(WindowLifecycleEvent lifecycleEvent)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);
        switch (lifecycleEvent.Kind)
        {
            case WindowLifecycleEventKind.Destroyed:
            case WindowLifecycleEventKind.Hidden:
                lock (gate)
                {
                    tracker.Forget(lifecycleEvent.WindowHandle);
                    lastReasons.Remove(lifecycleEvent.WindowHandle);
                }

                return;
            default:
                Evaluate(lifecycleEvent.WindowHandle);
                return;
        }
    }

    private void Evaluate(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return;
        }

        var environment = environmentFactory();
        var settings = environment.Configuration.Settings;
        var options = new ZoneFullscreenOptions(
            settings.ZoneFullscreen && SnapActivationPolicy.ShouldEnable(environment.Configuration),
            settings.SnappedTolerancePixels,
            settings.ZoneFullscreenMaxCorrections,
            CorrectionWindow);

        // Ausgeschaltet wird nichts gelesen und nichts gerechnet; der Vermerk zu diesem Fenster faellt
        // weg, damit ein spaeteres Einschalten mit einem sauberen Stand beginnt.
        if (!options.Enabled)
        {
            lock (gate)
            {
                tracker.Forget(windowHandle);
            }

            return;
        }

        if (reader.Read(windowHandle) is not { } state)
        {
            return;
        }

        var observation = new ZoneFullscreenObservation(
            state.Bounds,
            state.MonitorBounds,
            state.IsMaximized,
            state.IsMinimized,
            ZonesOn(environment, state.MonitorBounds),
            timeProvider.GetUtcNow(),
            state.IsBorderless);

        ZoneFullscreenDecision decision;
        lock (gate)
        {
            decision = tracker.Evaluate(windowHandle, observation, options);
        }

        // Die Nachschau laeuft mehrmals je Sekunde; derselbe Grund wird je Fenster nur einmal vermerkt.
        if (decision.Reason is { } reason)
        {
            bool isNew;
            lock (gate)
            {
                isNew = !lastReasons.TryGetValue(windowHandle, out var last) || last != reason;
                lastReasons[windowHandle] = reason;
            }

            if (isNew)
            {
                log($"Zonen-Vollbild für 0x{windowHandle:X}: {reason}");
            }
        }
        else
        {
            lock (gate)
            {
                lastReasons.Remove(windowHandle);
            }
        }

        if (decision.Action != ZoneFullscreenAction.HoldInZone)
        {
            return;
        }

        log($"Zonen-Vollbild für 0x{windowHandle:X}: Fenster {state.Bounds} auf Monitor {state.MonitorBounds}, rahmenlos={state.IsBorderless}, maximiert={state.IsMaximized}; Ziel {decision.Bounds}.");

        // Erst hier die teure Abfrage: die Fensteridentitaet wird nur gebraucht, wenn tatsaechlich
        // eingegriffen wuerde, und nicht bei jedem der vielen Lageereignisse.
        if (AppExclusionMatcher.IsExcluded(environment.Configuration.AppExclusions, identityReader(windowHandle)))
        {
            lock (gate)
            {
                tracker.Forget(windowHandle);
            }

            log($"Zonen-Vollbild für 0x{windowHandle:X} übersprungen: das Fenster ist ausgeschlossen.");
            return;
        }

        var outcome = fill(windowHandle, decision.Bounds);
        if (outcome.Succeeded)
        {
            log($"Zonen-Vollbild für 0x{windowHandle:X} auf {decision.Bounds} gesetzt.");
            return;
        }

        // Der Halt faellt weg, sonst bliebe das Fenster als «wird gehalten» vermerkt, obwohl es den
        // ganzen Monitor einnimmt. Die gemerkte Zone bleibt: ein Fenster, das sich nur um ein paar Pixel
        // gewehrt hat, sitzt sichtbar in seiner Zone und muss beim naechsten Rueckfall wieder dorthin.
        lock (gate)
        {
            tracker.CorrectionFailed(windowHandle);
        }

        log($"Zonen-Vollbild für 0x{windowHandle:X} nicht gesetzt: {outcome.Rejection}");
    }

    /// <summary>
    /// Die Zonen auf diesem Monitor. Gefiltert wird ueber die Flaeche statt ueber die Monitorkennung,
    /// weil die Monitorflaeche direkt vom Fenster stammt und keine Zuordnung ueber zwei Quellen braucht.
    /// </summary>
    private static IReadOnlyList<PlacementZoneTarget> ZonesOn(PlacementEnvironment environment, PixelRect monitorBounds)
    {
        var zones = new List<PlacementZoneTarget>();
        foreach (var zone in environment.Zones)
        {
            if (monitorBounds.Contains(zone.Bounds))
            {
                zones.Add(zone);
            }
        }

        return zones;
    }
}
