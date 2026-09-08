using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SnapZones.Core.Geometry;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Input;

/// <summary>
/// Uebergibt den echten Zeiger zwischen Zone und virtuellem Monitor. Ein Maus-Hook auf unterster Ebene
/// sieht jede Bewegung, bevor Windows sie anwendet: betritt der Zeiger die Zone, wird die Bewegung
/// verschluckt und der Zeiger auf den virtuellen Monitor gesetzt; verlaesst er dort einen Rand, kommt
/// er neben der Zone zurueck. Danach ist die Maus wirklich dort, wo das Programm sie vermutet — nichts
/// wird nachgebaut. Die Entscheidung trifft <see cref="CursorHandoffPlanner"/>; hier ist nur Windows.
///
/// <para>Der Hook gehoert auf den Thread mit der Nachrichtenschleife; dort wird er auch entfernt.</para>
/// </summary>
public sealed class CursorHandoff : IDisposable
{
    private const int MouseLowLevel = 14;
    private const int MouseMove = 0x0200;
    private static readonly TimeSpan LockAfterExit = TimeSpan.FromMilliseconds(150);
    private readonly Action<string, string> log;
    private readonly User32.LowLevelMouseProc procedure;
    private readonly nint hook;
    private readonly object gate = new();
    private PixelRect zone;
    private PixelRect virtualBounds;
    private MirrorPlan plan;
    private IReadOnlyList<PixelRect> monitors;
    private long lockedUntil;
    private PointInt lastSet = new(int.MinValue, int.MinValue);
    private bool disposed;

    public CursorHandoff(PixelRect zone, PixelRect virtualBounds, IReadOnlyList<PixelRect> monitors, Action<string, string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        this.log = log ?? ((_, _) => { });
        this.zone = zone;
        this.virtualBounds = virtualBounds;
        this.monitors = monitors;
        plan = MirrorGeometry.Plan(zone.Width, zone.Height, virtualBounds.Width, virtualBounds.Height);
        procedure = Callback;
        hook = User32.SetWindowsHookEx(MouseLowLevel, procedure, Kernel32.GetModuleHandle(null), 0);
        if (hook == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Der Maus-Hook liess sich nicht einrichten.");
        }
    }

    /// <summary>Ob der Zeiger gerade auf dem virtuellen Monitor ist.</summary>
    public bool InVirtual { get; private set; }

    public long Entries { get; private set; }

    public long Exits { get; private set; }

    /// <summary>Wird gemeldet, sobald der Zeiger die Seite wechselt; der Wert sagt, ob er jetzt virtuell ist.</summary>
    public event Action<bool>? SideChanged;

    /// <summary>Uebernimmt neue Rechtecke, etwa nach einem Moduswechsel oder einer Zonenaenderung.</summary>
    public void Update(PixelRect newZone, PixelRect newVirtualBounds, IReadOnlyList<PixelRect> newMonitors)
    {
        ArgumentNullException.ThrowIfNull(newMonitors);
        lock (gate)
        {
            zone = newZone;
            virtualBounds = newVirtualBounds;
            monitors = newMonitors;
            plan = MirrorGeometry.Plan(newZone.Width, newZone.Height, newVirtualBounds.Width, newVirtualBounds.Height);
        }
    }

    /// <summary>Holt den Zeiger sofort zurueck in die Zone — der Notausstieg.</summary>
    public void ForceExit()
    {
        PointInt target;
        lock (gate)
        {
            if (!InVirtual)
            {
                return;
            }

            User32.GetCursorPos(out var current);
            var inside = new PointInt(
                Math.Clamp(current.X, virtualBounds.X, virtualBounds.Right - 1),
                Math.Clamp(current.Y, virtualBounds.Y, virtualBounds.Bottom - 1));
            target = MirrorGeometry.ToZone(plan, zone, virtualBounds, inside);
            target = target with { Y = zone.Bottom + 1 };
            if (!monitors.Any(monitor => monitor.Contains(target)))
            {
                target = new PointInt(zone.X + zone.Width / 2, zone.Y - 2);
            }

            Leave(target);
        }

        SideChanged?.Invoke(false);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        ForceExit();
        User32.UnhookWindowsHookEx(hook);
        GC.KeepAlive(procedure);
    }

    private nint Callback(int code, nint wParam, nint lParam)
    {
        if (code < 0 || wParam != MouseMove || disposed)
        {
            return User32.CallNextHookEx(hook, code, wParam, lParam);
        }

        var info = Marshal.PtrToStructure<MouseLowLevelHookStruct>(lParam);
        var point = new PointInt(info.Point.X, info.Point.Y);
        bool? changedTo = null;
        lock (gate)
        {
            if (point == lastSet)
            {
                return User32.CallNextHookEx(hook, code, wParam, lParam);
            }

            var locked = Stopwatch.GetTimestamp() < lockedUntil;
            var decision = CursorHandoffPlanner.Decide(InVirtual, locked, zone, virtualBounds, plan, monitors, point);
            switch (decision.Action)
            {
                case HandoffAction.Enter:
                    InVirtual = true;
                    Entries++;
                    Move(decision.Target);
                    changedTo = true;
                    break;
                case HandoffAction.Exit:
                    Leave(decision.Target);
                    changedTo = false;
                    break;
                case HandoffAction.PushBack:
                    lockedUntil = Stopwatch.GetTimestamp() + (long)(LockAfterExit.TotalSeconds * Stopwatch.Frequency);
                    Move(decision.Target);
                    break;
                default:
                    return User32.CallNextHookEx(hook, code, wParam, lParam);
            }
        }

        if (changedTo is { } side)
        {
            SideChanged?.Invoke(side);
        }

        return 1;
    }

    private void Leave(PointInt target)
    {
        InVirtual = false;
        Exits++;
        lockedUntil = Stopwatch.GetTimestamp() + (long)(LockAfterExit.TotalSeconds * Stopwatch.Frequency);
        Move(target);
    }

    private void Move(PointInt target)
    {
        lastSet = target;
        if (!User32.SetCursorPos(target.X, target.Y))
        {
            log("WARN", $"Der Zeiger liess sich nicht auf {target.X},{target.Y} setzen.");
        }
    }
}
