using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;
using SnapZones.Core.Layouts;
using SnapZones.Core.AppRules;
using SnapZones.Core.PartMonitors;
using SnapZones.Windows.Native;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SnapZones.Windows.Windows;

public sealed class WindowsWindowService : IWindowService
{
    private const uint NonClientHitTest = 0x0084;
    private const int ExtendedFrameBoundsAttribute = 9;
    private const uint AbortIfHung = 0x0002;
    private const int Restore = 9;
    private const uint NoZOrder = 0x0004;
    private const uint NoActivate = 0x0010;
    private const uint NoOwnerZOrder = 0x0200;
    private const int VirtualKeyLeftButton = 0x01;

    private readonly Action<string>? trace;

    public WindowsWindowService(Action<string>? trace = null)
    {
        this.trace = trace;
    }

    public WindowSnapshot? Inspect(nint window, PointInt cursor, int ownProcessId)
    {
        if (window == 0 || !User32.IsWindow(window))
        {
            return null;
        }

        // Fuer ein vom Benutzer gezogenes Fenster reichen die Grundmerkmale; die Entscheidung trifft
        // WindowCandidateEvaluator. Nicht lesbare Stile gelten als «kein Kandidat».
        var root = User32.GetAncestor(window, WindowEligibility.RootAncestor);
        var isChild = root != 0 && root != window;
        User32.GetWindowThreadProcessId(window, out var processId);
        var style = User32.GetWindowLongPtr(window, WindowEligibility.StyleIndex).ToInt64();
        var extendedStyle = User32.GetWindowLongPtr(window, WindowEligibility.ExtendedStyleIndex).ToInt64();
        var cloakResult = DwmApi.DwmGetWindowAttribute(window, WindowEligibility.CloakedAttribute, out var cloaked, sizeof(int));

        return new WindowSnapshot(
            User32.IsWindowVisible(window),
            isChild || (style & WindowEligibility.ChildStyle) != 0,
            processId == (uint)ownProcessId,
            (extendedStyle & WindowEligibility.ToolWindowStyle) != 0 ||
                (extendedStyle & WindowEligibility.NoActivateStyle) != 0,
            cloakResult == 0 && cloaked != 0,
            IsTitleBarDrag(window, cursor),
            ReadAppIdentity(window, processId));
    }

    /// <summary>
    /// Der Fensterhelfer mit uiAccess, sofern eingerichtet. Er wird nur fuer Fenster gebraucht, die
    /// dieser Prozess selbst nicht bewegen darf.
    /// </summary>
    public Func<nint, PixelRect, bool>? ElevatedPlacement { get; set; }

    public bool TrySnap(nint window, PixelRect bounds) => Snap(window, bounds).Succeeded;

    /// <summary>
    /// Setzt das Fenster auf die Zone und misst nach. Fenster ohne veraenderbare Groesse werden in der
    /// Zone zentriert statt gestreckt. Weicht das Ergebnis ab, wird einmal wiederholt (ein Wechsel
    /// zwischen Monitoren mit unterschiedlicher Skalierung braucht haeufig zwei Anlaeufe), danach wird
    /// die Abweichung benannt statt verschwiegen.
    /// </summary>
    public PlacementOutcome Snap(nint window, PixelRect bounds)
    {
        if (window == 0 || !User32.IsWindow(window))
        {
            return PlacementOutcome.Rejected("Das Fenster ist nicht mehr vorhanden.");
        }

        if (bounds.Width < 1 || bounds.Height < 1)
        {
            return PlacementOutcome.Rejected("Die Zielzone hat keine Fläche.");
        }

        var target = FitToWindowCapabilities(window, bounds);
        var placement = CompensateInvisibleBorder(window, target);

        // Fenster hoeher berechtigter Programme gehen ueber den Helfer, alle uebrigen direkt. Der
        // Umweg kostet einen Prozesswechsel und lohnt sich nur dort, wo er noetig ist.
        if (RequiresElevation(window) && ElevatedPlacement is { } elevated && elevated(window, placement))
        {
            return Verify(window, placement, attemptAgain: null);
        }

        if (User32.IsIconic(window) || User32.IsZoomed(window))
        {
            _ = User32.ShowWindow(window, Restore);
        }

        if (!SetPosition(window, placement, out var error))
        {
            return PlacementOutcome.Rejected($"Windows hat die Platzierung abgelehnt ({error}).");
        }

        return Verify(window, placement, attemptAgain: () => SetPosition(window, placement, out _));
    }

    public WindowPlacementSnapshot? Capture(nint window)
    {
        if (!TryGetIdentity(window, out var identity))
        {
            return null;
        }

        var placement = new WindowPlacementNative { Length = (uint)Marshal.SizeOf<WindowPlacementNative>() };
        if (!User32.GetWindowPlacement(window, ref placement))
        {
            return null;
        }

        return new WindowPlacementSnapshot(
            identity, placement.Flags, placement.ShowCommand,
            new PointInt(placement.MinPosition.X, placement.MinPosition.Y),
            new PointInt(placement.MaxPosition.X, placement.MaxPosition.Y),
            WindowEligibility.ToPixelRect(placement.NormalPosition));
    }

    public bool TryApplyNormal(WindowIdentity identity, PixelRect bounds) => ApplyNormal(identity, bounds).Succeeded;

    public PlacementOutcome ApplyNormal(WindowIdentity identity, PixelRect bounds)
    {
        if (!MatchesCurrentIdentity(identity))
        {
            return PlacementOutcome.Rejected("Das Fenster ist nicht mehr dasselbe oder wurde geschlossen.");
        }

        return Snap(identity.Handle, bounds);
    }

    public bool TryRestore(WindowPlacementSnapshot snapshot)
    {
        if (!MatchesCurrentIdentity(snapshot.Identity))
        {
            return false;
        }

        var placement = new WindowPlacementNative
        {
            Length = (uint)Marshal.SizeOf<WindowPlacementNative>(),
            Flags = snapshot.Flags,
            ShowCommand = snapshot.ShowCommand,
            MinPosition = new PointNative { X = snapshot.MinPosition.X, Y = snapshot.MinPosition.Y },
            MaxPosition = new PointNative { X = snapshot.MaxPosition.X, Y = snapshot.MaxPosition.Y },
            NormalPosition = WindowEligibility.ToNativeRect(snapshot.NormalPosition)
        };
        return User32.SetWindowPlacement(snapshot.Identity.Handle, ref placement);
    }

    /// <summary>
    /// Alle Fenster, die ein Layoutwechsel mitfuehren darf: eigenstaendig, sichtbar, weder minimiert
    /// noch maximiert. Ein minimiertes Fenster wieder aufzuklappen oder ein maximiertes zu
    /// verkleinern, nur weil sich eine Zone verschoben hat, waere ein Eingriff, den niemand wollte.
    /// </summary>
    public IReadOnlyList<WindowPlacement> GetMovableTopLevelWindows(int ownProcessId)
    {
        var windows = new List<WindowPlacement>();
        _ = User32.EnumWindows((window, _) =>
        {
            if (WindowEligibility.TryClassify(window, ownProcessId, null, out var classification, out var reason) &&
                reason == WindowRejectionReason.None &&
                !classification.CloakStateUnknown &&
                !classification.IsMinimized &&
                !classification.IsMaximized)
            {
                windows.Add(new WindowPlacement(window, classification.Bounds));
            }

            return true;
        }, 0);
        return windows;
    }

    public WindowRuleCandidate? InspectRuleCandidate(nint window, int ownProcessId)
    {
        if (!WindowEligibility.TryClassify(window, ownProcessId, null, out var classification, out var reason) ||
            reason != WindowRejectionReason.None ||
            classification.CloakStateUnknown)
        {
            return null;
        }

        var processPath = WindowEligibility.ReadProcessPath(classification.ProcessId);
        if (processPath is null)
        {
            trace?.Invoke($"Fenster 0x{window:X} ({classification.WindowClass}): Programmpfad nicht lesbar, keine Regel anwendbar.");
            return null;
        }

        return new WindowRuleCandidate(
            window,
            new AppWindowIdentity(
                checked((int)classification.ProcessId),
                processPath,
                WindowEligibility.ReadWindowTitle(window),
                classification.WindowClass));
    }

    public IReadOnlyList<WindowRuleCandidate> GetRuleCandidates(int ownProcessId)
    {
        var windows = new List<WindowRuleCandidate>();
        _ = User32.EnumWindows((window, _) =>
        {
            var candidate = InspectRuleCandidate(window, ownProcessId);
            if (candidate is not null)
            {
                windows.Add(candidate);
            }

            return true;
        }, 0);
        return windows;
    }

    public bool TryGetCursorPosition(out PointInt point)
    {
        if (User32.GetCursorPos(out var nativePoint))
        {
            point = new PointInt(nativePoint.X, nativePoint.Y);
            return true;
        }

        point = default;
        return false;
    }

    public bool IsEscapePressed() => (User32.GetAsyncKeyState(0x1B) & 0x8000) != 0;

    public bool IsShiftPressed() => (User32.GetAsyncKeyState(0x10) & 0x8000) != 0;

    // 0x11 ist VK_CONTROL. Strg ist die einzige freie Zusatztaste beim Ziehen: Umschalt loest je
    // nach Einstellung das Einrasten aus, Alt schaltet den Magnetismus im Editor ab.
    public bool IsControlPressed() => (User32.GetAsyncKeyState(0x11) & 0x8000) != 0;

    public bool IsLeftButtonPressed() => (User32.GetAsyncKeyState(VirtualKeyLeftButton) & 0x8000) != 0;

    public bool IsWindowAlive(nint window) => window != 0 && User32.IsWindow(window);

    public (nint Handle, PixelRect Bounds)? GetForegroundWindow()
    {
        var window = User32.GetForegroundWindow();
        if (!WindowEligibility.TryClassify(window, Environment.ProcessId, null, out var classification, out var reason) ||
            reason != WindowRejectionReason.None ||
            classification.IsMinimized)
        {
            return null;
        }

        return (window, classification.Bounds);
    }

    public bool RequiresElevation(nint window)
    {
        if (window == 0 || !User32.IsWindow(window))
        {
            return false;
        }

        _ = User32.GetWindowThreadProcessId(window, out var processId);
        return processId != 0 && !WindowsIntegrityLevelReader.CanControl(processId);
    }

    /// <summary>
    /// Ein Fenster ohne veraenderbare Groesse (kein <c>WS_THICKFRAME</c>) kann eine Zone nicht fuellen.
    /// Es behaelt seine Groesse und wird in der Zone zentriert statt in deren Ecke gedrueckt.
    /// </summary>
    private static PixelRect FitToWindowCapabilities(nint window, PixelRect bounds)
    {
        var style = User32.GetWindowLongPtr(window, WindowEligibility.StyleIndex).ToInt64();
        if ((style & WindowEligibility.ThickFrameStyle) != 0 || !User32.GetWindowRect(window, out var current))
        {
            return bounds;
        }

        var size = WindowEligibility.ToPixelRect(current);
        if (size.Width < 1 || size.Height < 1)
        {
            return bounds;
        }

        return new PixelRect(0, 0, Math.Min(size.Width, bounds.Width), Math.Min(size.Height, bounds.Height))
            .CenteredIn(bounds);
    }

    private static bool SetPosition(nint window, PixelRect placement, out int error)
    {
        // Ohne SWP_ASYNCWINDOWPOS: nur ein synchroner Aufruf laesst sich unmittelbar danach nachmessen.
        var ok = User32.SetWindowPos(
            window,
            0,
            placement.X,
            placement.Y,
            placement.Width,
            placement.Height,
            NoZOrder | NoActivate | NoOwnerZOrder);
        error = ok ? 0 : Marshal.GetLastWin32Error();
        return ok;
    }

    private PlacementOutcome Verify(nint window, PixelRect expected, Func<bool>? attemptAgain)
    {
        if (!User32.GetWindowRect(window, out var measured))
        {
            return PlacementOutcome.Rejected("Das Fenster ist nach dem Setzen nicht mehr lesbar.");
        }

        var actual = WindowEligibility.ToPixelRect(measured);
        if (actual.IsWithinTolerance(expected, PlacementOutcome.TolerancePixels))
        {
            return PlacementOutcome.Success(actual);
        }

        if (attemptAgain is not null && attemptAgain() && User32.GetWindowRect(window, out measured))
        {
            actual = WindowEligibility.ToPixelRect(measured);
            if (actual.IsWithinTolerance(expected, PlacementOutcome.TolerancePixels))
            {
                trace?.Invoke($"Fenster 0x{window:X} sass erst im zweiten Anlauf in der Zone.");
                return PlacementOutcome.Success(actual);
            }
        }

        var reason = actual.Width > expected.Width + PlacementOutcome.TolerancePixels ||
            actual.Height > expected.Height + PlacementOutcome.TolerancePixels
                ? $"Das Fenster hält eine Mindestgrösse von {actual.Width} × {actual.Height} Pixeln ein und füllt die Zone nicht."
                : $"Windows hat das Fenster anders gesetzt als angefordert ({actual.Width} × {actual.Height} statt {expected.Width} × {expected.Height}).";
        trace?.Invoke($"Fenster 0x{window:X}: {reason} Ziel {expected}, Ergebnis {actual}.");
        return PlacementOutcome.Rejected(reason, actual);
    }

    /// <summary>
    /// Rechnet die Zielzone auf das Fensterrechteck um, damit der sichtbare Rahmen genau in der Zone
    /// liegt. Ohne diesen Ausgleich stehen Fenster in lückenlos aneinandergrenzenden Zonen sichtbar
    /// auseinander, weil Windows dem Fenster einen unsichtbaren Griffbereich zum Grössenziehen gibt.
    /// Ist der Rahmen nicht messbar oder unplausibel, bleibt die Zone unverändert.
    /// </summary>
    private static PixelRect CompensateInvisibleBorder(nint window, PixelRect bounds)
    {
        if (!User32.GetWindowRect(window, out var windowRectangle))
        {
            return bounds;
        }

        if (DwmApi.DwmGetWindowRectAttribute(
                window,
                ExtendedFrameBoundsAttribute,
                out var frame,
                Marshal.SizeOf<RectNative>()) != 0)
        {
            return bounds;
        }

        return WindowFrameCompensation.Apply(
            bounds,
            WindowEligibility.ToPixelRect(windowRectangle),
            WindowEligibility.ToPixelRect(frame));
    }

    /// <summary>
    /// Programm, Titel und Klasse des Fensters für die Auswertung von Ausschlüssen. Anders als
    /// <see cref="InspectRuleCandidate"/> gibt diese Abfrage auch dann eine Identität zurück, wenn der
    /// Programmpfad nicht lesbar ist — Windows verweigert ihn bei höher berechtigten Prozessen. Ein
    /// Ausschluss über Fenstertitel oder Fensterklasse greift dann trotzdem.
    /// </summary>
    private static AppWindowIdentity? ReadAppIdentity(nint window, uint processId)
    {
        var windowClass = WindowEligibility.ReadWindowClass(window);
        if (windowClass is null)
        {
            return null;
        }

        return new AppWindowIdentity(
            processId > int.MaxValue ? 0 : (int)processId,
            WindowEligibility.ReadProcessPath(processId) ?? string.Empty,
            WindowEligibility.ReadWindowTitle(window),
            windowClass);
    }

    private static bool MatchesCurrentIdentity(WindowIdentity expected) =>
        TryGetIdentity(expected.Handle, out var current) && current == expected;

    private static bool TryGetIdentity(nint window, out WindowIdentity identity)
    {
        identity = new WindowIdentity(0, 0, string.Empty);
        if (window == 0 || !User32.IsWindow(window))
        {
            return false;
        }

        _ = User32.GetWindowThreadProcessId(window, out var processId);
        var className = WindowEligibility.ReadWindowClass(window);
        if (processId == 0 || className is null)
        {
            return false;
        }

        identity = new WindowIdentity(window, processId, className);
        return true;
    }

    private static bool IsTitleBarDrag(nint window, PointInt cursor)
    {
        var packedPoint = (nint)(((long)(cursor.Y & 0xffff) << 16) | (uint)(cursor.X & 0xffff));
        var callResult = User32.SendMessageTimeout(
            window,
            NonClientHitTest,
            0,
            packedPoint,
            AbortIfHung,
            50,
            out var hitResult);
        if (callResult != 0)
        {
            return WindowHitTestClassifier.IsMoveOperation((int)hitResult);
        }

        if (!User32.GetWindowRect(window, out var rectangle))
        {
            return false;
        }

        // Rueckfall ohne Antwort des Fensters: Titelhoehe und Randbreite nach der Skalierung des
        // Monitors, auf dem das Fenster liegt, statt fester Pixelwerte.
        var dpi = 96u;
        var monitor = User32.MonitorFromWindow(window, User32.MonitorDefaultToNearest);
        if (monitor != 0 && Shcore.GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0)
        {
            dpi = Math.Max(96u, dpiX);
        }

        var scale = dpi / 96d;
        var border = (int)Math.Round(8 * scale);
        var captionHeight = (int)Math.Round(44 * scale);
        return cursor.X >= rectangle.Left + border &&
            cursor.X < rectangle.Right - border &&
            cursor.Y >= rectangle.Top + border / 2 &&
            cursor.Y < Math.Min(rectangle.Bottom, rectangle.Top + captionHeight);
    }

    /// <summary>Nur fuer Diagnosezwecke: Win32-Fehlercode als Text.</summary>
    internal static string DescribeError(int error) => new Win32Exception(error).Message;
}
