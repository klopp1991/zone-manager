using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SnapZones.Core.Geometry;
using SnapZones.Core.Monitors;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Displays;

/// <summary>Der virtuelle Monitor, wie Windows ihn gerade sieht.</summary>
/// <param name="DeviceName">Der GDI-Name (<c>\\.\DISPLAYn</c>); er wechselt bei jedem Anhaengen.</param>
/// <param name="Attached">Ob der Monitor am Desktop haengt.</param>
/// <param name="Bounds">Lage und Groesse im Desktopraum, solange er haengt.</param>
/// <param name="Dpi">Die wirksame Skalierung, solange er haengt.</param>
public sealed record VirtualDisplayState(string DeviceName, bool Attached, PixelRect Bounds, int RefreshRate, uint Dpi)
{
    public VirtualDisplayMode? Mode => Attached ? new VirtualDisplayMode(Bounds.Width, Bounds.Height) : null;
}

/// <summary>Ergebnis eines Moduswechsels; <see cref="Code"/> ist der Rueckgabewert von ChangeDisplaySettingsEx.</summary>
public sealed record DisplayChangeResult(bool Succeeded, int Code)
{
    public string Message => Code switch
    {
        0 => "Der Anzeigemodus wurde gesetzt.",
        1 => "Windows verlangt fuer diesen Anzeigemodus einen Neustart.",
        -2 => "Der Vollbildzonen-Treiber kennt diesen Modus nicht.",
        -3 => "Windows konnte die Anzeigeeinstellungen nicht schreiben.",
        -5 => "Windows hat die Modusangaben zurueckgewiesen.",
        _ => $"Der Anzeigemodus liess sich nicht setzen (Code {Code})."
    };
}

/// <summary>
/// Steuert den virtuellen Monitor des Vollbildzonen-Treibers ohne Administratorrechte: finden, Modeliste
/// nachfuehren, in Zonengroesse anhaengen, Skalierung des Zielmonitors uebernehmen, abhaengen.
///
/// <para>
/// Die Modeliste liest der Treiber aus <c>vdd_settings.xml</c>; neu einlesen laesst er sich ueber
/// seine Named Pipe mit dem Befehl <c>RELOAD_DRIVER</c>, wonach der Monitor kurz verschwindet und
/// mit der neuen Liste wieder eintrifft (gemessen rund drei Sekunden). Anhaengen und Abhaengen gehen
/// ueber <c>ChangeDisplaySettingsEx</c>, die Skalierung ueber die Anfragen der Einstellungen-App.
/// </para>
/// </summary>
public sealed class VirtualDisplayController
{
    private const uint QueryOnlyActivePaths = 0x00000002;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private readonly Action<string, string> log;

    public VirtualDisplayController(Action<string, string>? log = null)
    {
        this.log = log ?? ((_, _) => { });
    }

    /// <summary>Findet den virtuellen Monitor ueber die Hardware-Kennung seines Adapters, oder null.</summary>
    public static VirtualDisplayState? Find()
    {
        for (var index = 0u; ; index++)
        {
            var device = NewDisplayDevice();
            if (!User32.EnumDisplayDevices(null!, index, ref device, 0))
            {
                return null;
            }

            if ((device.StateFlags & User32.DisplayDeviceMirroringDriver) != 0 || !IsVirtualAdapter(device))
            {
                continue;
            }

            var attached = (device.StateFlags & User32.DisplayDeviceAttachedToDesktop) != 0;
            var bounds = default(PixelRect);
            var refreshRate = 0;
            var dpi = DisplayScaling.BaseDpi;
            var mode = NewDevMode();
            if (attached && User32.EnumDisplaySettingsEx(device.DeviceName, User32.EnumCurrentSettings, ref mode, 0))
            {
                bounds = new PixelRect(mode.PositionX, mode.PositionY, (int)mode.PelsWidth, (int)mode.PelsHeight);
                refreshRate = (int)mode.DisplayFrequency;
                dpi = ReadDpi(device.DeviceName);
            }

            return new VirtualDisplayState(device.DeviceName, attached, bounds, refreshRate, dpi);
        }
    }

    /// <summary>
    /// Wartet, bis das HMONITOR des Monitors eine Ruhepause lang unveraendert bleibt. Nach dem Anhaengen
    /// und nach einem Skalierungswechsel baut Windows die Monitorliste noch eine Weile um und vergibt
    /// dabei neue Handles; eine Aufnahme auf einem alten Handle liefert kein Bild.
    /// </summary>
    public static nint WaitForStableMonitorHandle(string deviceName, TimeSpan quietPeriod, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        var last = FindMonitorHandle(deviceName);
        var unchangedSince = Stopwatch.GetTimestamp();
        while (stopwatch.Elapsed < timeout)
        {
            Thread.Sleep(100);
            var current = FindMonitorHandle(deviceName);
            if (current != last)
            {
                last = current;
                unchangedSince = Stopwatch.GetTimestamp();
                continue;
            }

            if (current != 0 && Stopwatch.GetElapsedTime(unchangedSince) >= quietPeriod)
            {
                return current;
            }
        }

        return last;
    }

    /// <summary>
    /// Lage und Groesse aller angehaengten Monitore ausser dem virtuellen, in physischen Pixeln und
    /// unabhaengig von der DPI-Einstellung des aufrufenden Prozesses — daraus ergibt sich, wo der
    /// virtuelle Monitor hinkommt.
    /// </summary>
    public static IReadOnlyList<PixelRect> AttachedDisplayBounds()
    {
        var result = new List<PixelRect>();
        for (var index = 0u; ; index++)
        {
            var device = NewDisplayDevice();
            if (!User32.EnumDisplayDevices(null!, index, ref device, 0))
            {
                return result;
            }

            if ((device.StateFlags & User32.DisplayDeviceMirroringDriver) != 0 ||
                (device.StateFlags & User32.DisplayDeviceAttachedToDesktop) == 0 ||
                IsVirtualAdapter(device))
            {
                continue;
            }

            var mode = NewDevMode();
            if (User32.EnumDisplaySettingsEx(device.DeviceName, User32.EnumCurrentSettings, ref mode, 0) && mode.PelsWidth > 0)
            {
                result.Add(new PixelRect(mode.PositionX, mode.PositionY, (int)mode.PelsWidth, (int)mode.PelsHeight));
            }
        }
    }

    /// <summary>Die Modi, die der Treiber diesem Monitor gerade anbietet.</summary>
    public static IReadOnlyList<VirtualDisplayMode> AvailableModes(string deviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        var modes = new List<VirtualDisplayMode>();
        for (var index = 0; ; index++)
        {
            var mode = NewDevMode();
            if (!User32.EnumDisplaySettingsEx(deviceName, index, ref mode, 0))
            {
                break;
            }

            var candidate = new VirtualDisplayMode((int)mode.PelsWidth, (int)mode.PelsHeight);
            if (!modes.Contains(candidate))
            {
                modes.Add(candidate);
            }
        }

        return modes;
    }

    /// <summary>Das HMONITOR zu einem GDI-Namen, solange der Monitor haengt; sonst 0.</summary>
    public static nint FindMonitorHandle(string deviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        nint found = 0;
        User32.MonitorEnumProc callback = (monitor, _, _, _) =>
        {
            var info = new MonitorInfoEx { Size = (uint)Marshal.SizeOf<MonitorInfoEx>(), DeviceName = string.Empty };
            if (User32.GetMonitorInfo(monitor, ref info) &&
                string.Equals(info.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
            {
                found = monitor;
                return false;
            }

            return true;
        };
        User32.EnumDisplayMonitors(0, 0, callback, 0);
        return found;
    }

    /// <summary>
    /// Schreibt die Modeliste, wenn sie von der gespeicherten abweicht. Liefert wahr, wenn geschrieben
    /// wurde — dann liest der Treiber sie erst nach einem Neustart des Geraets (Administratorrechte,
    /// <see cref="VirtualDisplayDriverService.Restart"/>). Ein Neuladen ueber die Pipe des Treibers
    /// braeuchte keine Rechte, brachte ihn am 08.09.2026 aber nach rund zehn Aufrufen zum Absturz.
    /// </summary>
    public bool WriteModesIfChanged(IReadOnlyList<VirtualDisplayMode> modes)
    {
        ArgumentNullException.ThrowIfNull(modes);
        var xml = VirtualDisplayModes.ToSettingsXml(modes);
        var path = VirtualDisplayDriverService.SettingsPath;
        try
        {
            if (File.Exists(path) && File.ReadAllText(path) == xml)
            {
                return false;
            }

            File.WriteAllText(path, xml, Utf8WithoutBom);
            log("INFO", $"Modeliste des Vollbildzonen-Treibers mit {modes.Count} Modi geschrieben.");
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            log("ERROR", $"Die Modeliste liess sich nicht schreiben: {exception.Message}");
            return false;
        }
    }

    /// <summary>Ob der Monitor gerade alle diese Modi anbietet.</summary>
    public static bool ModesAvailable(string deviceName, IReadOnlyList<VirtualDisplayMode> modes)
    {
        ArgumentNullException.ThrowIfNull(modes);
        return ContainsAll(AvailableModes(deviceName), modes);
    }

    /// <summary>Wartet nach einem Geraeteneustart, bis der Monitor mit diesen Modi zurueck ist.</summary>
    public static VirtualDisplayState? WaitForModes(IReadOnlyList<VirtualDisplayMode> modes, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(modes);
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var state = Find();
            if (state is not null && ContainsAll(AvailableModes(state.DeviceName), modes))
            {
                return state;
            }

            if (stopwatch.Elapsed >= timeout)
            {
                return null;
            }

            Thread.Sleep(PollInterval);
        }
    }

    /// <summary>Haengt den Monitor mit diesem Modus an dieser Stelle an den Desktop.</summary>
    public DisplayChangeResult Attach(string deviceName, VirtualDisplayMode mode, PointInt position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ArgumentNullException.ThrowIfNull(mode);
        var devMode = NewDevMode();
        User32.EnumDisplaySettingsEx(deviceName, User32.EnumRegistrySettings, ref devMode, 0);
        devMode.Size = (ushort)Marshal.SizeOf<DevModeNative>();
        devMode.DriverExtra = 0;
        devMode.PelsWidth = (uint)mode.Width;
        devMode.PelsHeight = (uint)mode.Height;
        devMode.PositionX = position.X;
        devMode.PositionY = position.Y;
        devMode.DisplayFrequency = VirtualDisplayModes.RefreshRate;
        devMode.BitsPerPel = 32;
        devMode.Fields = User32.DmPelsWidth | User32.DmPelsHeight | User32.DmPosition | User32.DmDisplayFrequency | User32.DmBitsPerPel;
        return Apply(deviceName, ref devMode);
    }

    /// <summary>Haengt den Monitor vom Desktop ab; der Treiber bleibt, das Geraet bleibt.</summary>
    public DisplayChangeResult Detach(string deviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        var devMode = NewDevMode();
        User32.EnumDisplaySettingsEx(deviceName, User32.EnumRegistrySettings, ref devMode, 0);
        devMode.Size = (ushort)Marshal.SizeOf<DevModeNative>();
        devMode.DriverExtra = 0;
        devMode.PelsWidth = 0;
        devMode.PelsHeight = 0;
        devMode.PositionX = 0;
        devMode.PositionY = 0;
        devMode.Fields = User32.DmPelsWidth | User32.DmPelsHeight | User32.DmPosition;
        return Apply(deviceName, ref devMode);
    }

    /// <summary>Wartet, bis der Monitor mit dieser Groesse am Desktop haengt.</summary>
    public static VirtualDisplayState? WaitForAttached(VirtualDisplayMode mode, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(mode);
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var state = Find();
            if (state is { Attached: true } && state.Bounds.Width == mode.Width && state.Bounds.Height == mode.Height)
            {
                return state;
            }

            if (stopwatch.Elapsed >= timeout)
            {
                return null;
            }

            Thread.Sleep(PollInterval);
        }
    }

    /// <summary>Wartet, bis der Monitor vom Desktop verschwunden ist.</summary>
    public static bool WaitForDetached(TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var state = Find();
            if (state is null || !state.Attached)
            {
                return true;
            }

            if (stopwatch.Elapsed >= timeout)
            {
                return false;
            }

            Thread.Sleep(PollInterval);
        }
    }

    /// <summary>Die Skalierungsstufe des Monitors in Prozent, oder null, wenn Windows sie nicht nennt.</summary>
    public static int? ReadScalePercent(string deviceName)
    {
        if (!TryFindSource(deviceName, out var adapter, out var sourceId))
        {
            return null;
        }

        var request = NewGetDpiScale(adapter, sourceId);
        if (User32.DisplayConfigGetDpiScale(ref request) != 0)
        {
            return null;
        }

        var currentIndex = -request.MinimumScaleRelative + request.CurrentScaleRelative;
        return currentIndex >= 0 && currentIndex < DisplayScaling.Percentages.Count ? DisplayScaling.Percentages[currentIndex] : null;
    }

    /// <summary>
    /// Setzt die Skalierungsstufe des Monitors. Windows zaehlt Stufen relativ zur empfohlenen; die
    /// empfohlene ergibt sich aus dem kleinsten erlaubten Abstand, weil die unterste Stufe 100 % ist.
    /// </summary>
    public bool TrySetScalePercent(string deviceName, int percent)
    {
        var targetIndex = DisplayScaling.IndexOf(percent);
        if (targetIndex < 0 || !TryFindSource(deviceName, out var adapter, out var sourceId))
        {
            return false;
        }

        var request = NewGetDpiScale(adapter, sourceId);
        var read = User32.DisplayConfigGetDpiScale(ref request);
        if (read != 0)
        {
            log("WARN", $"Die Skalierung von {deviceName} liess sich nicht lesen (Code {read}).");
            return false;
        }

        var recommendedIndex = -request.MinimumScaleRelative;
        var relative = Math.Clamp(targetIndex - recommendedIndex, request.MinimumScaleRelative, request.MaximumScaleRelative);
        if (relative == request.CurrentScaleRelative)
        {
            return true;
        }

        var packet = new DisplayConfigSetDpiScaleNative
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = unchecked((uint)User32.DisplayConfigDeviceInfoSetDpiScale),
                Size = (uint)Marshal.SizeOf<DisplayConfigSetDpiScaleNative>(),
                AdapterId = adapter,
                Id = sourceId
            },
            ScaleRelative = relative
        };
        var written = User32.DisplayConfigSetDpiScale(ref packet);
        if (written != 0)
        {
            log("WARN", $"Die Skalierung von {deviceName} liess sich nicht auf {percent} % setzen (Code {written}).");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Wartet, bis Windows fuer den Monitor diese Skalierungsstufe nennt. Gelesen wird ueber die
    /// Anzeigekonfiguration, nicht ueber GetDpiForMonitor: das liefert einem nicht DPI-bewussten
    /// Prozess immer 96.
    /// </summary>
    public static bool WaitForScalePercent(string deviceName, int percent, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            if (ReadScalePercent(deviceName) == percent)
            {
                return true;
            }

            if (stopwatch.Elapsed >= timeout)
            {
                return false;
            }

            Thread.Sleep(PollInterval);
        }
    }

    public static uint ReadDpi(string deviceName)
    {
        var monitor = FindMonitorHandle(deviceName);
        if (monitor != 0 && Shcore.GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0)
        {
            return Math.Max(DisplayScaling.BaseDpi, dpiX);
        }

        return DisplayScaling.BaseDpi;
    }

    private DisplayChangeResult Apply(string deviceName, ref DevModeNative devMode)
    {
        var stopwatch = Stopwatch.StartNew();
        var code = User32.ChangeDisplaySettingsEx(deviceName, ref devMode, 0, User32.CdsUpdateRegistry | User32.CdsNoReset, 0);
        if (code == User32.DispChangeSuccessful)
        {
            code = User32.ChangeDisplaySettingsExApply(null, 0, 0, 0, 0);
        }

        var result = new DisplayChangeResult(code == User32.DispChangeSuccessful, code);
        log(result.Succeeded ? "INFO" : "ERROR",
            $"{deviceName}: {(devMode.PelsWidth == 0 ? "abgehaengt" : $"{devMode.PelsWidth}x{devMode.PelsHeight} bei ({devMode.PositionX},{devMode.PositionY})")} — {result.Message} ({stopwatch.ElapsedMilliseconds} ms)");
        return result;
    }

    private static bool TryFindSource(string deviceName, out LuidNative adapter, out uint sourceId)
    {
        adapter = default;
        sourceId = 0;
        if (User32.GetDisplayConfigBufferSizes(QueryOnlyActivePaths, out var pathCount, out var modeCount) != 0)
        {
            return false;
        }

        var paths = new DisplayConfigPathInfo[pathCount];
        var modes = new DisplayConfigModeInfo[modeCount];
        if (User32.QueryDisplayConfig(QueryOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, 0) != 0)
        {
            return false;
        }

        for (var index = 0; index < pathCount; index++)
        {
            var source = new DisplayConfigSourceDeviceName
            {
                Header = new DisplayConfigDeviceInfoHeader
                {
                    Type = 1,
                    Size = (uint)Marshal.SizeOf<DisplayConfigSourceDeviceName>(),
                    AdapterId = paths[index].SourceInfo.AdapterId,
                    Id = paths[index].SourceInfo.Id
                },
                ViewGdiDeviceName = string.Empty
            };
            if (User32.DisplayConfigGetSourceDeviceInfo(ref source) == 0 &&
                string.Equals(source.ViewGdiDeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
            {
                adapter = paths[index].SourceInfo.AdapterId;
                sourceId = paths[index].SourceInfo.Id;
                return true;
            }
        }

        return false;
    }

    private static DisplayConfigGetDpiScaleNative NewGetDpiScale(LuidNative adapter, uint sourceId) => new()
    {
        Header = new DisplayConfigDeviceInfoHeader
        {
            Type = unchecked((uint)User32.DisplayConfigDeviceInfoGetDpiScale),
            Size = (uint)Marshal.SizeOf<DisplayConfigGetDpiScaleNative>(),
            AdapterId = adapter,
            Id = sourceId
        }
    };

    private static bool ContainsAll(IReadOnlyList<VirtualDisplayMode> available, IReadOnlyList<VirtualDisplayMode> wanted) =>
        wanted.All(available.Contains);

    private static bool IsVirtualAdapter(DisplayDevice device) =>
        string.Equals(device.DeviceId, VirtualDisplay.AdapterHardwareId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(device.DeviceString, VirtualDisplay.AdapterDescription, StringComparison.OrdinalIgnoreCase);

    private static DisplayDevice NewDisplayDevice() => new()
    {
        Size = Marshal.SizeOf<DisplayDevice>(),
        DeviceName = string.Empty,
        DeviceString = string.Empty,
        DeviceId = string.Empty,
        DeviceKey = string.Empty
    };

    private static DevModeNative NewDevMode() => new()
    {
        Size = (ushort)Marshal.SizeOf<DevModeNative>(),
        DeviceName = string.Empty,
        FormName = string.Empty
    };
}
