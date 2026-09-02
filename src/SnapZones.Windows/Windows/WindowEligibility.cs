using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using SnapZones.Core.Geometry;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Windows;

/// <summary>Was ueber ein Top-Level-Fenster bekannt ist, bevor entschieden wird, ob es angefasst wird.</summary>
internal sealed record WindowClassification(
    uint ProcessId,
    long Style,
    long ExtendedStyle,
    string WindowClass,
    PixelRect Bounds,
    bool IsCloaked,
    bool CloakStateUnknown,
    bool IsMinimized,
    bool IsMaximized)
{
    public bool IsResizable => (Style & WindowEligibility.ThickFrameStyle) != 0;
}

/// <summary>Warum ein Fenster nicht in Frage kommt; fuer Protokoll und Fehlersuche.</summary>
internal enum WindowRejectionReason
{
    None,
    NotAWindow,
    Invisible,
    NotTopLevel,
    ToolWindow,
    NoActivate,
    BorderlessPopup,
    Cloaked,
    ShellWindow,
    OwnProcess,
    Unreadable,
    Empty
}

/// <summary>
/// Der eine gemeinsame Fensterfilter fuer Regeln, Auffang, Positionsgedaechtnis und Layoutwechsel.
/// Bis zum 02.09.2026 hatten zwei Fensterdienste zwei verschiedene Vorstellungen davon, was ein
/// geeignetes Fenster ist; Splash-Screens, Overlays fremder Programme und randlose Popups kamen so
/// in den Katalog und wurden beim naechsten Oeffnen verschoben.
/// </summary>
internal static class WindowEligibility
{
    internal const int StyleIndex = -16;
    internal const int ExtendedStyleIndex = -20;
    internal const long ChildStyle = 0x40000000L;
    internal const long PopupStyle = 0x80000000L;
    internal const long CaptionStyle = 0x00C00000L;
    internal const long ThickFrameStyle = 0x00040000L;
    internal const long ToolWindowStyle = 0x00000080L;
    internal const long NoActivateStyle = 0x08000000L;
    internal const uint RootAncestor = 2;
    internal const int CloakedAttribute = 14;

    private static readonly IWindowStyleReader DefaultStyleReader = new User32WindowStyleReader();

    private static readonly HashSet<string> ShellWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow"
    };

    /// <summary>
    /// Liest Stil, Prozess, Klasse und Zustand eines Fensters und prueft die harten Kriterien: sichtbar,
    /// eigenstaendig, kein Werkzeugfenster, kein Fenster ohne Aktivierung, kein randloses Popup, kein
    /// Shell-Fenster, nicht der eigene Prozess. Cloaking (virtuelle Desktops, UWP-Hintergrund) wird
    /// gemeldet, nicht entschieden: fuer das automatische Platzieren zaehlt es als Ausschluss, fuer ein
    /// vom Benutzer gezogenes Fenster nicht.
    /// </summary>
    public static bool TryClassify(
        nint window,
        int ownProcessId,
        IWindowStyleReader? styleReader,
        out WindowClassification classification,
        out WindowRejectionReason reason)
    {
        classification = default!;
        reason = WindowRejectionReason.None;
        if (window == 0 || !User32.IsWindow(window))
        {
            reason = WindowRejectionReason.NotAWindow;
            return false;
        }

        if (!User32.IsWindowVisible(window))
        {
            reason = WindowRejectionReason.Invisible;
            return false;
        }

        var root = User32.GetAncestor(window, RootAncestor);
        if (root != 0 && root != window)
        {
            reason = WindowRejectionReason.NotTopLevel;
            return false;
        }

        var reader = styleReader ?? DefaultStyleReader;
        if (!reader.TryRead(window, StyleIndex, out var style) ||
            !reader.TryRead(window, ExtendedStyleIndex, out var extendedStyle))
        {
            reason = WindowRejectionReason.Unreadable;
            return false;
        }

        if ((style & ChildStyle) != 0)
        {
            reason = WindowRejectionReason.NotTopLevel;
            return false;
        }

        if ((extendedStyle & ToolWindowStyle) != 0)
        {
            reason = WindowRejectionReason.ToolWindow;
            return false;
        }

        if ((extendedStyle & NoActivateStyle) != 0)
        {
            reason = WindowRejectionReason.NoActivate;
            return false;
        }

        // Ein Popup ohne Titelleiste und ohne Rahmen ist ein Splash-Screen, ein Overlay oder ein
        // Vollbildspiel. Keines davon gehoert in eine Zone.
        if ((style & PopupStyle) != 0 && (style & CaptionStyle) != CaptionStyle && (style & ThickFrameStyle) == 0)
        {
            reason = WindowRejectionReason.BorderlessPopup;
            return false;
        }

        _ = User32.GetWindowThreadProcessId(window, out var processId);
        if (processId == 0)
        {
            reason = WindowRejectionReason.Unreadable;
            return false;
        }

        if (processId == (uint)ownProcessId)
        {
            reason = WindowRejectionReason.OwnProcess;
            return false;
        }

        var windowClass = ReadWindowClass(window);
        if (windowClass is null)
        {
            reason = WindowRejectionReason.Unreadable;
            return false;
        }

        if (ShellWindowClasses.Contains(windowClass))
        {
            reason = WindowRejectionReason.ShellWindow;
            return false;
        }

        if (!User32.GetWindowRect(window, out var rectangle))
        {
            reason = WindowRejectionReason.Unreadable;
            return false;
        }

        var bounds = new PixelRect(
            rectangle.Left,
            rectangle.Top,
            Math.Max(0, rectangle.Right - rectangle.Left),
            Math.Max(0, rectangle.Bottom - rectangle.Top));
        if (bounds.Width < 1 || bounds.Height < 1)
        {
            reason = WindowRejectionReason.Empty;
            return false;
        }

        var cloakResult = DwmApi.DwmGetWindowAttribute(window, CloakedAttribute, out var cloaked, sizeof(int));
        classification = new WindowClassification(
            processId,
            style,
            extendedStyle,
            windowClass,
            bounds,
            cloakResult == 0 && cloaked != 0,
            cloakResult != 0,
            User32.IsIconic(window),
            User32.IsZoomed(window));
        if (classification.IsCloaked)
        {
            reason = WindowRejectionReason.Cloaked;
        }

        return true;
    }

    public static string? ReadWindowClass(nint window)
    {
        var className = new StringBuilder(256);
        return User32.GetClassName(window, className, className.Capacity) > 0
            ? className.ToString()
            : null;
    }

    public static string ReadWindowTitle(nint window)
    {
        var capacity = Math.Clamp(User32.GetWindowTextLength(window) + 1, 1, 32768);
        var title = new StringBuilder(capacity);
        _ = User32.GetWindowText(window, title, capacity);
        return title.ToString();
    }

    /// <summary>
    /// Der vollstaendige Pfad der Programmdatei ueber <c>QueryFullProcessImageName</c>. Anders als
    /// <c>Process.MainModule</c> funktioniert das auch bei geschuetzten und bei 32-Bit-Prozessen.
    /// </summary>
    public static string? ReadProcessPath(uint processId)
    {
        using SafeProcessHandle process = Kernel32.OpenProcess(Kernel32.QueryLimitedInformation, false, processId);
        if (process.IsInvalid)
        {
            return null;
        }

        var capacity = 32768;
        var path = new StringBuilder(capacity);
        return Kernel32.QueryFullProcessImageName(process, 0, path, ref capacity)
            ? path.ToString()
            : null;
    }

    public static PixelRect ToPixelRect(RectNative rectangle) => new(
        rectangle.Left,
        rectangle.Top,
        rectangle.Right - rectangle.Left,
        rectangle.Bottom - rectangle.Top);

    public static RectNative ToNativeRect(PixelRect rectangle) => new()
    {
        Left = rectangle.X,
        Top = rectangle.Y,
        Right = rectangle.Right,
        Bottom = rectangle.Bottom
    };
}
