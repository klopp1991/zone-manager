using SnapZones.Core.Geometry;
using SnapZones.Core.Placement;

namespace SnapZones.Core.Fullscreen;

/// <summary>Was mit einem Fenster geschehen soll, das gerade beobachtet wurde.</summary>
public enum ZoneFullscreenAction
{
    /// <summary>Nichts tun.</summary>
    None,

    /// <summary>Das Fenster auf die Flaeche der Zone setzen, in der es vor dem Vollbild lag.</summary>
    HoldInZone
}

/// <param name="Bounds">Die Zielflaeche; nur bei <see cref="ZoneFullscreenAction.HoldInZone"/> gesetzt.</param>
/// <param name="Reason">Warum nichts geschieht, sofern es einen nennenswerten Grund gibt; sonst <c>null</c>.</param>
public readonly record struct ZoneFullscreenDecision(
    ZoneFullscreenAction Action,
    PixelRect Bounds,
    string? Reason = null)
{
    public static ZoneFullscreenDecision Nothing => new(ZoneFullscreenAction.None, default);

    public static ZoneFullscreenDecision Skip(string reason) => new(ZoneFullscreenAction.None, default, reason);

    public static ZoneFullscreenDecision Hold(PixelRect bounds) => new(ZoneFullscreenAction.HoldInZone, bounds);
}

/// <param name="Enabled">Ob das Zonen-Vollbild ueberhaupt gilt.</param>
/// <param name="SnappedTolerancePixels">Ab welcher Naehe zu den Zonenkanten ein Fenster als eingerastet gilt.</param>
/// <param name="MaximumCorrections">
/// Wie oft dasselbe Fenster je Vollbildsitzung zurueckgeholt wird. Manche Programme setzen ihr Fenster
/// nach dem Umschalten mehrfach; ein Programm, das sich hartnaeckig wehrt, darf aber keinen Dauerkampf
/// ausloesen. Danach behaelt es sein Monitorvollbild.
/// </param>
/// <param name="CorrectionWindow">Nach dieser Ruhezeit ohne Korrektur beginnt die Zaehlung von vorn.</param>
public sealed record ZoneFullscreenOptions(
    bool Enabled,
    int SnappedTolerancePixels,
    int MaximumCorrections,
    TimeSpan CorrectionWindow)
{
    public static ZoneFullscreenOptions Disabled { get; } =
        new(false, 40, 5, TimeSpan.FromSeconds(5));
}

/// <param name="WindowBounds">Das Fensterrechteck aus <c>GetWindowRect</c>.</param>
/// <param name="MonitorBounds">Die ganze Flaeche des Monitors, auf dem das Fenster liegt.</param>
/// <param name="Zones">Die Zonen der aktiven Layouts auf diesem Monitor.</param>
/// <param name="IsBorderless">
/// Ob das Fenster weder Titelleiste noch Griffrahmen hat. Ein Programm im Vollbild legt beide ab; solange
/// sie fehlen, gilt sein Vollbild als nicht verlassen, auch wenn das Rechteck gerade weder die Zone noch
/// den Monitor trifft.
/// </param>
public readonly record struct ZoneFullscreenObservation(
    PixelRect WindowBounds,
    PixelRect MonitorBounds,
    bool IsMaximized,
    bool IsMinimized,
    IReadOnlyList<PlacementZoneTarget> Zones,
    DateTimeOffset TimestampUtc,
    bool IsBorderless = false);

/// <summary>
/// Haelt je Fenster fest, in welcher Zone es zuletzt eingerastet lag, und entscheidet daraus, ob ein
/// Vollbild auf diese Zone zurueckgeholt wird.
///
/// <para>
/// Zurueckgeholt wird nur, was vorher in einer Zone lag. Ein Fenster, das der Benutzer frei auf dem
/// Bildschirm abgelegt hat, geht weiterhin auf den ganzen Monitor — sonst haette das Zonen-Vollbild
/// keinen erkennbaren Bezugspunkt und wuerde Programme an Stellen zwingen, die niemand gewaehlt hat.
/// </para>
///
/// <para>
/// Der Zustand je Fenster ist noetig, weil das eigene Zuruecksetzen selbst wieder ein Fensterereignis
/// ausloest. Ohne die Erinnerung an die gehaltene Flaeche wuerde jede Korrektur als neues Einrasten
/// gelesen, der Zaehler liefe zurueck, und ein sich wehrendes Programm koennte einen Dauerkampf
/// ausloesen.
/// </para>
///
/// <para>Die Klasse rechnet nur; das Setzen des Fensters bleibt beim Aufrufer.</para>
/// </summary>
public sealed class ZoneFullscreenTracker
{
    /// <summary>
    /// Wie weit das Fenster von der gehaltenen Flaeche abweichen darf und noch als sitzend gilt.
    /// Grosszuegiger als die Platzierungstoleranz, weil ein Programm im Vollbild seine Groesse leicht
    /// anpassen kann.
    /// </summary>
    private const int HoldTolerancePixels = 8;

    private readonly Dictionary<nint, WindowState> states = [];

    /// <summary>Die Anzahl beobachteter Fenster. Nur fuer Tests und Diagnose.</summary>
    public int TrackedWindows => states.Count;

    /// <summary>
    /// Bewertet eine Beobachtung und liefert, was zu tun ist. Wird fuer jedes Fensterereignis gerufen.
    /// </summary>
    public ZoneFullscreenDecision Evaluate(
        nint window,
        ZoneFullscreenObservation observation,
        ZoneFullscreenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (window == 0)
        {
            return ZoneFullscreenDecision.Nothing;
        }

        if (!options.Enabled)
        {
            states.Remove(window);
            return ZoneFullscreenDecision.Nothing;
        }

        // Ein minimiertes Fenster sagt nichts ueber seine Zone aus; sein Rechteck liegt weit ausserhalb
        // des Monitors. Der bisherige Stand bleibt, damit es beim Zurueckholen seine Zone wiederfindet.
        if (observation.IsMinimized)
        {
            return ZoneFullscreenDecision.Nothing;
        }

        var state = states.TryGetValue(window, out var known) ? known : new WindowState();
        var decision = EvaluateCore(state, observation, options);

        // Vermerkt wird nur ein Fenster, das etwas zu erinnern hat. Die Ereignisse kommen von jedem
        // Fenster der Sitzung — Menues, Hinweisfenster, die Shell selbst —, und ein Eintrag je Fenster
        // waere ein Gedaechtnis, das ueber die Laufzeit nur waechst. Fenster ohne Zone verschwinden
        // dadurch sofort wieder, statt auf ihr Schliessen zu warten.
        if (state.SnappedArea is null && state.HeldArea is null)
        {
            states.Remove(window);
        }
        else
        {
            states[window] = state;
        }

        return decision;
    }

    private static ZoneFullscreenDecision EvaluateCore(
        WindowState state,
        ZoneFullscreenObservation observation,
        ZoneFullscreenOptions options)
    {
        if (state.HeldArea is { } held)
        {
            return EvaluateWhileHolding(state, held, observation, options);
        }

        if (!IsMonitorFullscreen(observation))
        {
            RememberSnappedArea(state, observation, options);
            return ZoneFullscreenDecision.Nothing;
        }

        if (state.SnappedArea is not { } zoneArea)
        {
            // Das Fenster lag vor dem Vollbild in keiner Zone. Sein Vollbild bleibt das des Monitors.
            return ZoneFullscreenDecision.Nothing;
        }

        // Deckt die Zone selbst den ganzen Monitor ab, gibt es nichts zurueckzuholen. Ohne diese Pruefung
        // verbrauchte ein Layout mit einer einzigen bildschirmfuellenden Zone bei jedem Vollbild das
        // Korrekturbudget, ohne je etwas zu bewegen.
        if (observation.WindowBounds.IsWithinTolerance(zoneArea, HoldTolerancePixels))
        {
            return ZoneFullscreenDecision.Nothing;
        }

        return Correct(state, zoneArea, observation, options);
    }

    /// <summary>Vergisst ein Fenster, etwa wenn es geschlossen wurde.</summary>
    public void Forget(nint window) => states.Remove(window);

    /// <summary>
    /// Vermerkt, dass das letzte Zurueckholen nicht gegriffen hat. Der Halt faellt weg, damit das
    /// naechste Ereignis einen neuen Versuch bewerten kann; die gemerkte Zone und der Zaehler bleiben.
    /// Wuerde das Fenster ganz vergessen, ginge mit dem Halt auch der Bezugspunkt verloren, und ein
    /// Fenster, das sichtbar in seiner Zone sitzt, waere nie wieder zurueckzuholen.
    /// </summary>
    public void CorrectionFailed(nint window)
    {
        if (states.TryGetValue(window, out var state))
        {
            state.HeldArea = null;
            if (state.SnappedArea is null)
            {
                states.Remove(window);
            }
        }
    }

    /// <summary>Vergisst alle Fenster, etwa nach einem Layoutwechsel oder beim Abschalten.</summary>
    public void Clear() => states.Clear();

    /// <summary>Ob dieses Fenster gerade in seiner Zone im Vollbild gehalten wird.</summary>
    public bool IsHeld(nint window) => states.TryGetValue(window, out var state) && state.HeldArea is not null;

    /// <summary>
    /// Die Fenster, die gerade gehalten werden. Fuer die Nachschau per Zeitgeber: ein Programm kann sein
    /// Fenster zurueck auf den Monitor setzen, ohne dass dafuer ein Ereignis ankommt.
    /// </summary>
    public IReadOnlyList<nint> HeldWindows
    {
        get
        {
            var held = new List<nint>();
            foreach (var (window, state) in states)
            {
                if (state.HeldArea is not null)
                {
                    held.Add(window);
                }
            }

            return held;
        }
    }

    private static ZoneFullscreenDecision EvaluateWhileHolding(
        WindowState state,
        PixelRect held,
        ZoneFullscreenObservation observation,
        ZoneFullscreenOptions options)
    {
        // Das Fenster sitzt, wo es hingesetzt wurde — der Normalfall und meist das Echo der eigenen
        // Korrektur.
        if (observation.WindowBounds.IsWithinTolerance(held, HoldTolerancePixels))
        {
            return ZoneFullscreenDecision.Nothing;
        }

        // Das Programm hat sich erneut auf den ganzen Monitor gesetzt.
        if (IsMonitorFullscreen(observation))
        {
            return Correct(state, held, observation, options);
        }

        // Ohne Titelleiste und Griffrahmen versteht sich das Programm weiterhin als Vollbild. Chromium
        // stellt bei jedem Aktivierungswechsel einen Teil seiner Vollbildgroesse wieder her, oft nur
        // die Breite. Das ist kein Verlassen des Vollbilds, sondern ein halber Rueckfall, und wird wie
        // ein ganzer behandelt. Wuerde die verfaelschte Flaeche hier als neues Einrasten gelesen,
        // vergiftete sie den Bezugspunkt fuer alle spaeteren Korrekturen.
        if (observation.IsBorderless)
        {
            return Correct(state, held, observation, options);
        }

        // Weder die gehaltene Flaeche noch Monitorvollbild, und der Rahmen ist zurueck: das Programm
        // hat sein Vollbild verlassen. Von hier an gilt wieder die gewoehnliche Beobachtung.
        state.HeldArea = null;
        state.Corrections = 0;
        state.FirstCorrectionUtc = null;
        RememberSnappedArea(state, observation, options);
        return ZoneFullscreenDecision.Nothing;
    }

    private static ZoneFullscreenDecision Correct(
        WindowState state,
        PixelRect target,
        ZoneFullscreenObservation observation,
        ZoneFullscreenOptions options)
    {
        var limit = Math.Max(1, options.MaximumCorrections);
        var window = options.CorrectionWindow > TimeSpan.Zero ? options.CorrectionWindow : TimeSpan.FromSeconds(5);
        if (state.FirstCorrectionUtc is { } first && observation.TimestampUtc - first > window)
        {
            state.Corrections = 0;
            state.FirstCorrectionUtc = null;
        }

        if (state.Corrections >= limit)
        {
            return ZoneFullscreenDecision.Skip(
                $"Das Vollbild wurde {limit}-mal in Folge auf den ganzen Monitor zurückgesetzt; das Fenster behält es.");
        }

        state.Corrections++;
        state.FirstCorrectionUtc ??= observation.TimestampUtc;
        state.HeldArea = target;
        return ZoneFullscreenDecision.Hold(target);
    }

    private static void RememberSnappedArea(
        WindowState state,
        ZoneFullscreenObservation observation,
        ZoneFullscreenOptions options)
    {
        var snapped = ZoneFullscreen.FindSnappedArea(
            observation.WindowBounds,
            observation.Zones,
            options.SnappedTolerancePixels);

        // Ein rahmenloses Fenster ausserhalb jeder Zone ist meist ein Vollbild auf dem Weg zu seiner
        // Groesse, kein bewusst abgelegtes Fenster. Die zuletzt gemerkte Zone bleibt dann stehen.
        if (snapped is null && observation.IsBorderless)
        {
            return;
        }

        state.SnappedArea = snapped;
    }

    /// <summary>
    /// Ob das Fenster den ganzen Monitor einnimmt, ohne maximiert zu sein. Ein maximiertes Fenster endet
    /// gewoehnlich an der Taskleiste; bei ausgeblendeter Taskleiste deckt es den Monitor ebenfalls, ist
    /// aber kein Vollbild und wird nicht angefasst.
    /// </summary>
    private static bool IsMonitorFullscreen(ZoneFullscreenObservation observation) =>
        !observation.IsMaximized &&
        ZoneFullscreen.CoversMonitor(observation.WindowBounds, observation.MonitorBounds);

    private sealed class WindowState
    {
        /// <summary>Die Zonenflaeche, auf der das Fenster zuletzt ohne Vollbild eingerastet lag.</summary>
        public PixelRect? SnappedArea { get; set; }

        /// <summary>Die Flaeche, auf der das Fenster gerade im Vollbild gehalten wird.</summary>
        public PixelRect? HeldArea { get; set; }

        public int Corrections { get; set; }

        public DateTimeOffset? FirstCorrectionUtc { get; set; }
    }
}
