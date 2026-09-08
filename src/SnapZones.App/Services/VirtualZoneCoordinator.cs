using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using SnapZones.App.Overlays;
using SnapZones.Core.Geometry;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.PartMonitors;
using SnapZones.Windows.Capture;
using SnapZones.Windows.Displays;
using SnapZones.Windows.Hooks;
using SnapZones.Windows.Input;
using SnapZones.Windows.Windows;

namespace SnapZones.App.Services;

/// <summary>
/// Haelt ein Fenster in einer Vollbildzone: sobald ein Fenster in eine als virtueller Monitor
/// gekennzeichnete Zone kommt, wird der virtuelle Monitor des Anzeigetreibers in Zonengroesse
/// angehaengt, die Skalierung des Zielmonitors uebernommen, das Fenster dorthin gelegt, sein Bild in
/// die Zone gespiegelt und der Zeiger uebergeben. Verlaesst das Fenster den virtuellen Monitor oder
/// wird es geschlossen, endet die Sitzung, und der Monitor wird abgehaengt. Es gibt hoechstens eine
/// Sitzung; ein zweites Fenster loest das erste ab.
///
/// <para>
/// Alles Langsame — Modeliste nachfuehren, anhaengen, skalieren — laeuft im Hintergrund; Fenster,
/// Spiegel und Zeiger-Hook gehoeren auf den UI-Thread. Ohne Anzeigetreiber tut der Koordinator nichts
/// als den Hinweis, wie er sich einschalten laesst.
/// </para>
/// </summary>
public sealed class VirtualZoneCoordinator : IDisposable
{
    private const int EscapeHotkeyId = 1;
    private const int HotkeyMessage = 0x0312;
    private const uint ModifierAlt = 0x0001;
    private const uint ModifierControl = 0x0002;
    private const uint ModifierNoRepeat = 0x4000;
    private const uint EscapeKey = 0x51; // Q
    private static readonly TimeSpan ModeTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan AttachTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ScaleTimeout = TimeSpan.FromSeconds(3);
    private readonly IWindowService windowService;
    private readonly Func<SnapConfiguration> configuration;
    private readonly Func<IReadOnlyList<PartMonitorTarget>> targets;
    private readonly Dispatcher dispatcher;
    private readonly Action<string, string, Exception?> log;
    private readonly Action<string> status;
    private readonly Func<Task<bool>> restartDriver;
    private readonly VirtualDisplayController controller;
    private readonly DispatcherTimer watchdog;
    private Session? session;
    private bool starting;
    private bool disposed;

    /// <param name="restartDriver">
    /// Startet das Geraet des Anzeigetreibers neu (Administratorrechte) und liefert, ob das gelungen
    /// ist. Noetig, wenn die Modeliste eine neue Zonengroesse bekommt oder der Treiber einen Fehler meldet.
    /// </param>
    public VirtualZoneCoordinator(
        IWindowService windowService,
        Func<SnapConfiguration> configuration,
        Func<IReadOnlyList<PartMonitorTarget>> targets,
        Dispatcher dispatcher,
        Action<string, string, Exception?> log,
        Action<string> status,
        Func<Task<bool>> restartDriver)
    {
        this.windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.status = status ?? throw new ArgumentNullException(nameof(status));
        this.restartDriver = restartDriver ?? throw new ArgumentNullException(nameof(restartDriver));
        controller = new VirtualDisplayController((level, message) => this.log(level, message, null));
        watchdog = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(1) };
        watchdog.Tick += (_, _) => Watch();
    }

    /// <summary>Ob gerade ein Fenster in einer Vollbildzone gehalten wird.</summary>
    public bool IsActive => session is not null;

    /// <summary>Das gehaltene Fenster, oder 0.</summary>
    public nint ActiveWindow => session?.Window ?? 0;

    /// <summary>
    /// Beim Start: ein virtueller Monitor, der nach einem harten Prozessende noch am Desktop haengt,
    /// wird abgehaengt. Sonst bliebe ein Geistermonitor in den Anzeigeeinstellungen.
    /// </summary>
    public void CleanUpOrphan()
    {
        _ = Task.Run(() =>
        {
            try
            {
                var state = VirtualDisplayController.Find();
                if (state is { Attached: true })
                {
                    log("INFO", $"Ein verwaister virtueller Monitor ({state.DeviceName}) hängt noch am Desktop und wird abgehängt.", null);
                    controller.Detach(state.DeviceName);
                }
            }
            catch (Exception exception) when (exception is Win32Exception or COMException or InvalidOperationException)
            {
                log("WARN", "Der verwaiste virtuelle Monitor liess sich nicht abhängen.", exception);
            }
        });
    }

    /// <summary>
    /// Nach einer Platzierung: liegt das Ziel in einer Vollbildzone, beginnt die Sitzung. Liefert
    /// wahr, wenn die Zone eine Vollbildzone ist — unabhaengig davon, ob die Sitzung gelingt.
    /// </summary>
    public bool TryEnter(nint window, string monitorStableId, Guid zoneId)
    {
        if (disposed || window == 0)
        {
            return false;
        }

        var target = targets().FirstOrDefault(candidate => candidate.Monitor.Identity.StableId == monitorStableId);
        var zone = target?.PartMonitors.FirstOrDefault(candidate => candidate.Id == zoneId);
        if (target is null || zone is null || !zone.IsVirtualMonitor)
        {
            return false;
        }

        if (session is { } active && active.Window == window && active.ZoneId == zoneId)
        {
            return true;
        }

        _ = StartAsync(window, target, zone);
        return true;
    }

    /// <summary>Fensterereignisse des gehaltenen Fensters; laeuft auf dem UI-Thread.</summary>
    public void Handle(WindowLifecycleEvent lifecycleEvent)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);
        if (session is not { } active || lifecycleEvent.WindowHandle != active.Window)
        {
            return;
        }

        switch (lifecycleEvent.Kind)
        {
            case WindowLifecycleEventKind.Destroyed:
            case WindowLifecycleEventKind.Hidden:
                End("das Fenster wurde geschlossen", moveBack: false);
                break;
            case WindowLifecycleEventKind.MoveSizeEnded:
            case WindowLifecycleEventKind.LocationChanged:
                CheckWindowStillThere();
                break;
        }
    }

    /// <summary>Die Zonen wurden neu aufgebaut: die Sitzung folgt der Zone oder endet mit ihr.</summary>
    public void Reconfigure()
    {
        if (session is not { } active)
        {
            return;
        }

        var target = targets().FirstOrDefault(candidate => candidate.Monitor.Identity.StableId == active.MonitorStableId);
        var zone = target?.PartMonitors.FirstOrDefault(candidate => candidate.Id == active.ZoneId);
        if (target is null || zone is null || !zone.IsVirtualMonitor)
        {
            End("die Zone gibt es nicht mehr");
            return;
        }

        var bounds = ZoneBounds(target, zone);
        if (bounds == active.ZoneBounds)
        {
            return;
        }

        var window = active.Window;
        End("die Zone hat sich geändert", moveBack: false);
        _ = StartAsync(window, target, zone);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        watchdog.Stop();
        var active = session;
        session = null;
        if (active is null)
        {
            return;
        }

        try
        {
            TearDown(active, moveBack: true);
            controller.Detach(active.Display.DeviceName);
        }
        catch (Exception exception) when (exception is Win32Exception or COMException or InvalidOperationException or ObjectDisposedException)
        {
            log("WARN", "Die Vollbildzone liess sich beim Beenden nicht sauber abbauen.", exception);
        }
    }

    private async Task StartAsync(nint window, PartMonitorTarget target, ZoneDefinition zone)
    {
        if (starting)
        {
            return;
        }

        starting = true;
        try
        {
            End("ein anderes Fenster übernimmt die Vollbildzone");
            var driver = VirtualDisplayDriverService.ReadStatus();
            if (!driver.DevicePresent || !driver.ConfigurationPresent)
            {
                status("Vollbildzone: der Anzeigetreiber fehlt. Programm → Anzeigetreiber für Vollbildzonen richtet ihn ein.");
                return;
            }

            if (!MonitorMirror.IsSupported())
            {
                status("Vollbildzone: diese Windows-Version bietet die Bildschirmaufnahme ohne Rahmen nicht an.");
                return;
            }

            var zoneBounds = ZoneBounds(target, zone);
            var mode = VirtualDisplayModes.Normalize(zoneBounds.Width, zoneBounds.Height);
            var modes = VirtualDisplayModes.Build([mode, .. VirtualZoneSizes()]);
            var percent = DisplayScaling.NearestPercent(target.Monitor.DpiX);
            status($"Vollbildzone «{zone.Name}» wird vorbereitet …");

            var prepared = await PrepareAsync(driver, mode, modes, percent);
            if (prepared is null)
            {
                status("Vollbildzone: der virtuelle Monitor liess sich nicht anhängen. Einzelheiten stehen im Protokoll.");
                return;
            }

            if (!await MonitorMirror.RequestBorderlessAsync())
            {
                log("WARN", "Windows erlaubt die Aufnahme ohne Rahmen nicht; der Spiegel zeigt einen gelben Rahmen.", null);
            }

            if (windowService.Capture(window) is null)
            {
                SafeDetach(prepared.DeviceName);
                status("Vollbildzone: das Fenster ist nicht mehr da.");
                return;
            }

            var outcome = windowService.Fill(window, prepared.Bounds);
            if (!outcome.Succeeded)
            {
                SafeDetach(prepared.DeviceName);
                status($"Vollbildzone: das Fenster liess sich nicht auf den virtuellen Monitor legen. {outcome.Rejection}");
                return;
            }

            // Windows baut die Monitorliste nach Anhaengen und Skalieren noch eine Weile um; erst ein
            // Handle, das eine Ruhepause lang gleich bleibt, taugt als Aufnahmeziel.
            var device = prepared.DeviceName;
            var monitorHandle = await Task.Run(() => VirtualDisplayController.WaitForStableMonitorHandle(device, TimeSpan.FromMilliseconds(750), TimeSpan.FromSeconds(5)));
            if (monitorHandle == 0)
            {
                windowService.Snap(window, zoneBounds);
                SafeDetach(prepared.DeviceName);
                status("Vollbildzone: der virtuelle Monitor ist nach dem Anhängen nicht auffindbar.");
                return;
            }

            var mirrorWindow = new MirrorWindow();
            mirrorWindow.ShowAt(zoneBounds);
            var created = new Session(window, zone.Id, target.Monitor.Identity.StableId, zoneBounds, prepared, mirrorWindow);
            created.Mirror = CreateMirror(created);
            created.Mirror.Start();

            try
            {
                created.Cursor = new CursorHandoff(zoneBounds, prepared.Bounds, VirtualDisplayController.AttachedDisplayBounds(), (level, message) => log(level, message, null));
            }
            catch (Win32Exception exception)
            {
                log("WARN", "Die Zeigerübergabe liess sich nicht einrichten; der Zeiger bleibt auf dem echten Monitor.", exception);
            }

            mirrorWindow.MessageReceived += message =>
            {
                if (message.Msg == HotkeyMessage && (int)message.WParam == EscapeHotkeyId)
                {
                    created.Cursor?.ForceExit();
                }
            };
            created.HotkeyRegistered = RegisterHotKey(mirrorWindow.Handle, EscapeHotkeyId, ModifierControl | ModifierAlt | ModifierNoRepeat, EscapeKey);
            if (!created.HotkeyRegistered)
            {
                log("WARN", "Strg+Alt+Q ist schon belegt; der Notausstieg des Zeigers steht nicht zur Verfügung.", null);
            }

            session = created;
            watchdog.Start();
            log("INFO", $"Vollbildzone «{zone.Name}»: Fenster 0x{window:X} liegt auf {prepared.DeviceName} {prepared.Bounds}, Spiegel in {zoneBounds}, Skalierung {percent} %.", null);
            status($"Fenster in Vollbildzone «{zone.Name}». Strg+Alt+Q holt den Zeiger zurück.");
        }
        catch (Exception exception) when (exception is Win32Exception or COMException or InvalidOperationException or SharpGen.Runtime.SharpGenException)
        {
            log("ERROR", "Die Vollbildzone liess sich nicht einrichten.", exception);
            status($"Vollbildzone fehlgeschlagen: {exception.Message}");
            var stranded = VirtualDisplayController.Find();
            if (stranded is { Attached: true })
            {
                SafeDetach(stranded.DeviceName);
            }
        }
        finally
        {
            starting = false;
        }
    }

    /// <summary>
    /// Modeliste, Geraeteneustart bei Bedarf, Anhaengen, Skalierung. Liefert den Monitor, wie er
    /// haengt. Das Langsame laeuft im Hintergrund; der Neustart des Geraets braucht Administratorrechte
    /// und laeuft ueber den erhoehten Hilfsprozess, nur wenn die Modeliste neu ist, der Treiber einen
    /// Fehler meldet oder der Monitor die Modi nicht kennt.
    /// </summary>
    private async Task<VirtualDisplayState?> PrepareAsync(VirtualDisplayDriverStatus driver, VirtualDisplayMode mode, IReadOnlyList<VirtualDisplayMode> modes, int percent)
    {
        var listChanged = await Task.Run(() => controller.WriteModesIfChanged(modes));
        var state = await Task.Run(VirtualDisplayController.Find);
        var needsRestart = listChanged || driver.Faulted || state is null || !VirtualDisplayController.ModesAvailable(state.DeviceName, modes);
        if (needsRestart)
        {
            log("INFO", driver.Faulted
                ? $"Der Anzeigetreiber meldet Code {driver.ProblemCode}; das Gerät wird neu gestartet."
                : "Der Anzeigetreiber bekommt eine neue Modeliste; das Gerät wird neu gestartet.", null);
            status("Vollbildzone: der Anzeigetreiber wird neu gestartet; Windows fragt nach Administratorrechten …");
            if (!await restartDriver())
            {
                log("ERROR", "Der Anzeigetreiber liess sich nicht neu starten.", null);
                return null;
            }

            state = await Task.Run(() => VirtualDisplayController.WaitForModes(modes, ModeTimeout));
            if (state is null)
            {
                log("ERROR", "Der virtuelle Monitor ist nach dem Neustart nicht mit der Modeliste zurückgekommen.", null);
                return null;
            }
        }

        if (state is null)
        {
            return null;
        }

        var device = state.DeviceName;
        return await Task.Run(() =>
        {
            var position = VirtualDisplayPlacement.ChoosePosition(VirtualDisplayController.AttachedDisplayBounds());
            if (!controller.Attach(device, mode, position).Succeeded)
            {
                return null;
            }

            var attached = VirtualDisplayController.WaitForAttached(mode, AttachTimeout);
            if (attached is null)
            {
                log("ERROR", "Der virtuelle Monitor ist nach dem Anhängen nicht erschienen.", null);
                return null;
            }

            if (controller.TrySetScalePercent(attached.DeviceName, percent))
            {
                VirtualDisplayController.WaitForScalePercent(attached.DeviceName, percent, ScaleTimeout);
            }

            return VirtualDisplayController.Find() is { Attached: true } final ? final : attached;
        });
    }

    private void Watch()
    {
        if (session is not { } active)
        {
            watchdog.Stop();
            return;
        }

        active.Ticks++;
        // Drei Sekunden nach dem Start (und nach jedem Neustart) muss mindestens ein Bild da sein:
        // schon das erste Bild eines stehenden Desktops liefert die Aufnahme sofort.
        if (active.Ticks % 4 == 3 && active.Mirror.TotalFrames == 0)
        {
            RestartMirror(active);
            if (session != active)
            {
                return;
            }
        }

        if (active.Ticks % 10 == 0)
        {
            var statistics = active.Mirror.ReadStatistics();
            log("DEBUG", $"Spiegel: {statistics.FramesPerSecond:0.0} Bilder/s, {active.Mirror.TotalFrames} gesamt; Zeiger {(active.Cursor?.InVirtual == true ? "virtuell" : "echt")}, Eintritte {active.Cursor?.Entries ?? 0}, Austritte {active.Cursor?.Exits ?? 0}.", null);
        }

        CheckWindowStillThere();
    }

    private void CheckWindowStillThere()
    {
        if (session is not { } active)
        {
            return;
        }

        var current = windowService.Capture(active.Window);
        if (current is null)
        {
            End("das Fenster ist verschwunden", moveBack: false);
            return;
        }

        var rect = current.NormalPosition;
        var centre = new PointInt(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        if (!active.Display.Bounds.Contains(centre))
        {
            End("das Fenster hat den virtuellen Monitor verlassen", moveBack: false);
        }
    }

    private void End(string reason, bool moveBack = true)
    {
        var active = session;
        if (active is null)
        {
            return;
        }

        session = null;
        watchdog.Stop();
        log("INFO", $"Vollbildzone beendet: {reason}.", null);
        TearDown(active, moveBack);
        var device = active.Display.DeviceName;
        _ = Task.Run(() => SafeDetach(device));
    }

    private void TearDown(Session active, bool moveBack)
    {
        if (active.HotkeyRegistered)
        {
            UnregisterHotKey(active.MirrorWindow.Handle, EscapeHotkeyId);
        }

        active.Cursor?.Dispose();
        active.Mirror.Dispose();
        active.MirrorWindow.Close();
        active.MirrorWindow.Dispose();
        if (moveBack && windowService.Capture(active.Window) is not null)
        {
            windowService.Snap(active.Window, active.ZoneBounds);
        }
    }

    private void SafeDetach(string deviceName)
    {
        try
        {
            controller.Detach(deviceName);
        }
        catch (Exception exception) when (exception is Win32Exception or COMException or InvalidOperationException)
        {
            log("WARN", "Der virtuelle Monitor liess sich nicht abhängen.", exception);
        }
    }

    private PixelRect ZoneBounds(PartMonitorTarget target, ZoneDefinition zone)
    {
        var settings = configuration().Settings;
        return ZoneGeometry.ToPixels(zone.Bounds, target.Monitor.WorkArea, new LayoutMetrics(settings.EffectiveOuterMargins, settings.ZoneGap));
    }

    /// <summary>Die Groessen aller Vollbildzonen der aktiven Layouts, damit die Modeliste jede kennt.</summary>
    private IEnumerable<VirtualDisplayMode> VirtualZoneSizes()
    {
        foreach (var target in targets())
        {
            foreach (var zone in target.PartMonitors.Where(candidate => candidate.IsVirtualMonitor))
            {
                var bounds = ZoneBounds(target, zone);
                yield return VirtualDisplayModes.Normalize(bounds.Width, bounds.Height);
            }
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int id);

    private MonitorMirror CreateMirror(Session active)
    {
        var device = active.Display.DeviceName;
        var mirror = new MonitorMirror(
            active.MirrorWindow.Handle,
            active.ZoneBounds.Width,
            active.ZoneBounds.Height,
            () => VirtualDisplayController.FindMonitorHandle(device),
            (level, message) => log(level, message, null));
        mirror.Lost += () => _ = dispatcher.InvokeAsync(() =>
        {
            if (session == active && ReferenceEquals(active.Mirror, mirror))
            {
                End("die Aufnahme des virtuellen Monitors ist verloren gegangen");
            }
        });
        return mirror;
    }

    /// <summary>
    /// Ein Spiegel ohne Bilder haengt an einem veralteten Monitor-Handle; ein neuer Spiegel loest das
    /// Handle frisch auf. Hoechstens zweimal, sonst endet die Sitzung mit einem Hinweis.
    /// </summary>
    private void RestartMirror(Session active)
    {
        if (active.MirrorRestarts >= 2)
        {
            End("der Spiegel bekommt keine Bilder vom virtuellen Monitor");
            status("Vollbildzone beendet: der Spiegel bekam kein Bild. Einzelheiten stehen im Protokoll.");
            return;
        }

        active.MirrorRestarts++;
        log("WARN", $"Der Spiegel hat kein Bild bekommen und wird neu gestartet ({active.MirrorRestarts}. Mal).", null);
        try
        {
            active.Mirror.Dispose();
            active.Mirror = CreateMirror(active);
            active.Mirror.Start();
        }
        catch (Exception exception) when (exception is InvalidOperationException or COMException or SharpGen.Runtime.SharpGenException)
        {
            log("ERROR", "Der Spiegel liess sich nicht neu starten.", exception);
            End("der Spiegel liess sich nicht neu starten");
        }
    }

    private sealed class Session(
        nint window,
        Guid zoneId,
        string monitorStableId,
        PixelRect zoneBounds,
        VirtualDisplayState display,
        MirrorWindow mirrorWindow)
    {
        public nint Window { get; } = window;
        public Guid ZoneId { get; } = zoneId;
        public string MonitorStableId { get; } = monitorStableId;
        public PixelRect ZoneBounds { get; } = zoneBounds;
        public VirtualDisplayState Display { get; } = display;
        public MirrorWindow MirrorWindow { get; } = mirrorWindow;
        public MonitorMirror Mirror { get; set; } = null!;
        public CursorHandoff? Cursor { get; set; }
        public bool HotkeyRegistered { get; set; }
        public int Ticks { get; set; }
        public int MirrorRestarts { get; set; }
        public long FramesAtLastCheck { get; set; }
    }
}
