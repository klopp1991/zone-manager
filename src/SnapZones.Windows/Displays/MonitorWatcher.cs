using System.Windows.Interop;
using Microsoft.Win32;

namespace SnapZones.Windows.Displays;

/// <summary>
/// Meldet, wenn sich die Monitore aendern: Anstecken, Abstecken, Aufloesung, Skalierung, Drehung oder
/// eine verschobene Taskleiste. Bis zum 02.09.2026 wurden die Monitore genau einmal beim Start gelesen;
/// danach rechneten Overlays und Zonen mit veralteten Koordinaten, bis das Programm neu startete.
///
/// Windows meldet eine Aenderung mehrfach kurz hintereinander (je Monitor, je Modus). Die Meldungen
/// werden gesammelt und erst nach einer Ruhepause weitergegeben.
/// </summary>
public sealed class MonitorWatcher : IDisposable
{
    private const int DisplayChangeMessage = 0x007E;
    private const int SettingChangeMessage = 0x001A;
    private const int SetWorkAreaParameter = 0x002F;
    private readonly SynchronizationContext synchronizationContext;
    private readonly TimeSpan quietPeriod;
    private readonly Timer debounce;
    private readonly object gate = new();
    private HwndSource? source;
    private bool disposed;

    public MonitorWatcher(SynchronizationContext synchronizationContext, TimeSpan? quietPeriod = null)
    {
        this.synchronizationContext = synchronizationContext ?? throw new ArgumentNullException(nameof(synchronizationContext));
        this.quietPeriod = quietPeriod ?? TimeSpan.FromMilliseconds(750);
        debounce = new Timer(_ => Fire(), null, Timeout.Infinite, Timeout.Infinite);
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        source = new HwndSource(new HwndSourceParameters("ZoneManager.MonitorWatcher")
        {
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x80000000)
        });
        source.AddHook(ProcessMessage);
    }

    /// <summary>Wird auf dem UI-Thread ausgeloest, fruehestens nach der Ruhepause seit der letzten Meldung.</summary>
    public event Action? Changed;

    /// <summary>Fuer Tests und den Diagnosemodus: eine Aenderung von aussen anstossen.</summary>
    public void Trigger()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            debounce.Change(quietPeriod, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
        }

        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        debounce.Dispose();
        if (source is not null)
        {
            source.RemoveHook(ProcessMessage);
            source.Dispose();
            source = null;
        }
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs eventArgs) => Trigger();

    private nint ProcessMessage(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        _ = window;
        _ = lParam;
        if (message == DisplayChangeMessage ||
            (message == SettingChangeMessage && wParam.ToInt64() == SetWorkAreaParameter))
        {
            Trigger();
        }

        return 0;
    }

    private void Fire()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
        }

        synchronizationContext.Post(_ => Changed?.Invoke(), null);
    }
}
