using System.Diagnostics;
using System.Runtime.InteropServices;
using SnapZones.Core.Geometry;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Security.Authorization.AppCapabilityAccess;
using WinRT;

namespace SnapZones.Windows.Capture;

[ComImport]
[Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IGraphicsCaptureItemInterop
{
    nint CreateForWindow(nint window, ref Guid iid);

    nint CreateForMonitor(nint monitor, ref Guid iid);
}

[ComImport]
[Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDirect3DDxgiInterfaceAccess
{
    nint GetInterface(ref Guid iid);
}

/// <summary>Bildzaehlung des Spiegels seit dem letzten Ablesen.</summary>
public sealed record MirrorStatistics(long Frames, double Seconds)
{
    public double FramesPerSecond => Seconds > 0 ? Frames / Seconds : 0;
}

/// <summary>
/// Spiegelt den Inhalt eines Monitors in ein Fenster: Aufnahme ueber Windows.Graphics.Capture, Ausgabe
/// ueber eine Direct3D-11-Swapchain auf dem Fenster. Der Zeiger wird von der Aufnahme mitgezeichnet,
/// der gelbe Aufnahmerahmen ist abgeschaltet. Bei bewegtem Inhalt liefert die Aufnahme jedes Bild,
/// bei stehendem keines — der Spiegel kostet dann nichts.
///
/// <para>
/// Nichts Langsames gehoert in den Bildpfad: das Kopieren eines Bildes in den Hauptspeicher hat im
/// Prototyp den Spiegel auf ein Viertel der Bildrate gedrueckt. Hier wird nur von Textur zu Textur
/// kopiert und praesentiert.
/// </para>
/// </summary>
public sealed class MonitorMirror : IDisposable
{
    private static readonly Guid GraphicsCaptureItemInterfaceId = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid Texture2DInterfaceId = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");
    private readonly object gate = new();
    private readonly Action<string, string> log;
    private readonly nint targetWindow;
    private readonly nint monitor;
    private ID3D11Device? device;
    private ID3D11DeviceContext? context;
    private IDXGISwapChain1? swapChain;
    private ID3D11Texture2D? backBuffer;
    private IDirect3DDevice? captureDevice;
    private GraphicsCaptureItem? item;
    private Direct3D11CaptureFramePool? framePool;
    private GraphicsCaptureSession? session;
    private SizeInt32 contentSize;
    private int targetWidth;
    private int targetHeight;
    private long frames;
    private long statisticsStart;
    private bool lost;
    private bool disposed;

    public MonitorMirror(nint targetWindow, int targetWidth, int targetHeight, nint monitor, Action<string, string>? log = null)
    {
        if (targetWindow == 0)
        {
            throw new ArgumentException("Das Zielfenster fehlt.", nameof(targetWindow));
        }

        if (monitor == 0)
        {
            throw new ArgumentException("Der Monitor fehlt.", nameof(monitor));
        }

        this.targetWindow = targetWindow;
        this.monitor = monitor;
        this.targetWidth = Math.Max(1, targetWidth);
        this.targetHeight = Math.Max(1, targetHeight);
        this.log = log ?? ((_, _) => { });
    }

    /// <summary>Die Aufnahme ist verloren: der Monitor wurde abgehaengt oder das Geraet ist weg.</summary>
    public event Action? Lost;

    /// <summary>Ob Windows die Bildschirmaufnahme und den rahmenlosen Modus anbietet.</summary>
    public static bool IsSupported()
    {
        try
        {
            return GraphicsCaptureSession.IsSupported() &&
                global::Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent("Windows.Graphics.Capture.GraphicsCaptureSession", "IsBorderRequired");
        }
        catch (Exception exception) when (exception is COMException or TypeLoadException or NotImplementedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Bittet Windows um die Aufnahme ohne gelben Rahmen. Fuer eine unverpackte Programmdatei kommt
    /// die Antwort ohne Rueckfrage; die Abfrage ist trotzdem Pflicht, sonst bleibt der Rahmen.
    /// </summary>
    public static async Task<bool> RequestBorderlessAsync()
    {
        try
        {
            var status = await GraphicsCaptureAccess.RequestAccessAsync(GraphicsCaptureAccessKind.Borderless);
            return status == AppCapabilityAccessStatus.Allowed;
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or TypeLoadException)
        {
            return false;
        }
    }

    /// <summary>Baut Geraet, Swapchain und Aufnahme auf und beginnt zu spiegeln.</summary>
    public void Start()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (session is not null)
            {
                return;
            }

            D3D11.D3D11CreateDevice(
                null,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0],
                out device!,
                out context!).CheckError();

            using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
            Direct3D11Interop.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var inspectable).CheckError();
            captureDevice = MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
            Marshal.Release(inspectable);

            using var adapter = dxgiDevice.GetAdapter();
            using var factory = adapter.GetParent<IDXGIFactory2>();
            swapChain = factory.CreateSwapChainForHwnd(device, targetWindow, new SwapChainDescription1
            {
                Width = (uint)targetWidth,
                Height = (uint)targetHeight,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = 2,
                SwapEffect = SwapEffect.FlipDiscard,
                Scaling = Scaling.Stretch,
                AlphaMode = AlphaMode.Ignore
            });
            backBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0);

            item = CreateItemForMonitor(monitor);
            item.Closed += (_, _) => RaiseLost("Der Monitor der Aufnahme ist verschwunden.");
            contentSize = item.Size;
            framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(captureDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, contentSize);
            framePool.FrameArrived += OnFrameArrived;
            session = framePool.CreateCaptureSession(item);
            session.IsCursorCaptureEnabled = true;
            try
            {
                session.IsBorderRequired = false;
            }
            catch (Exception exception) when (exception is COMException or UnauthorizedAccessException)
            {
                log("WARN", $"Der Aufnahmerahmen liess sich nicht abschalten: {exception.Message}");
            }

            statisticsStart = Stopwatch.GetTimestamp();
            session.StartCapture();
            log("INFO", $"Spiegel gestartet: {contentSize.Width}x{contentSize.Height} in {targetWidth}x{targetHeight}.");
        }
    }

    /// <summary>Passt die Ausgabe an eine neue Zonengroesse an.</summary>
    public void Resize(int width, int height)
    {
        lock (gate)
        {
            if (disposed || swapChain is null || (width == targetWidth && height == targetHeight))
            {
                return;
            }

            targetWidth = Math.Max(1, width);
            targetHeight = Math.Max(1, height);
            backBuffer?.Dispose();
            backBuffer = null;
            swapChain.ResizeBuffers(2, (uint)targetWidth, (uint)targetHeight, Format.B8G8R8A8_UNorm, SwapChainFlags.None);
            backBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0);
        }
    }

    /// <summary>Bilder seit dem letzten Ablesen; setzt die Zaehlung zurueck.</summary>
    public MirrorStatistics ReadStatistics()
    {
        var now = Stopwatch.GetTimestamp();
        var count = Interlocked.Exchange(ref frames, 0);
        var start = Interlocked.Exchange(ref statisticsStart, now);
        return new MirrorStatistics(count, (now - start) / (double)Stopwatch.Frequency);
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
            try
            {
                session?.Dispose();
            }
            catch (Exception exception) when (exception is COMException or ObjectDisposedException)
            {
            }

            try
            {
                framePool?.Dispose();
            }
            catch (Exception exception) when (exception is COMException or ObjectDisposedException)
            {
            }

            session = null;
            framePool = null;
            item = null;
            backBuffer?.Dispose();
            swapChain?.Dispose();
            context?.Dispose();
            device?.Dispose();
            backBuffer = null;
            swapChain = null;
            context = null;
            device = null;
            captureDevice = null;
        }
    }

    private static GraphicsCaptureItem CreateItemForMonitor(nint monitor)
    {
        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        var iid = GraphicsCaptureItemInterfaceId;
        var pointer = interop.CreateForMonitor(monitor, ref iid);
        try
        {
            return GraphicsCaptureItem.FromAbi(pointer);
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        try
        {
            using var frame = sender.TryGetNextFrame();
            if (frame is null)
            {
                return;
            }

            lock (gate)
            {
                if (disposed || context is null || backBuffer is null || swapChain is null || framePool is null || captureDevice is null)
                {
                    return;
                }

                if (frame.ContentSize.Width != contentSize.Width || frame.ContentSize.Height != contentSize.Height)
                {
                    contentSize = frame.ContentSize;
                    framePool.Recreate(captureDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, contentSize);
                    return;
                }

                var access = frame.Surface.As<IDirect3DDxgiInterfaceAccess>();
                var iid = Texture2DInterfaceId;
                using var texture = new ID3D11Texture2D(access.GetInterface(ref iid));
                var plan = MirrorGeometry.Plan(targetWidth, targetHeight, contentSize.Width, contentSize.Height);
                if (plan.IsExact && plan.Width == contentSize.Width && plan.Height == contentSize.Height)
                {
                    context.CopyResource(backBuffer, texture);
                }
                else
                {
                    context.CopySubresourceRegion(
                        backBuffer,
                        0,
                        (uint)plan.DestinationX,
                        (uint)plan.DestinationY,
                        0,
                        texture,
                        0,
                        new Box(plan.SourceX, plan.SourceY, 0, plan.SourceX + plan.Width, plan.SourceY + plan.Height, 1));
                }

                swapChain.Present(1, PresentFlags.None);
                Interlocked.Increment(ref frames);
            }
        }
        catch (Exception exception) when (exception is COMException or SharpGen.Runtime.SharpGenException or ObjectDisposedException or InvalidOperationException)
        {
            RaiseLost($"Der Spiegel hat die Aufnahme verloren: {exception.Message}");
        }
    }

    private void RaiseLost(string reason)
    {
        lock (gate)
        {
            if (lost || disposed)
            {
                return;
            }

            lost = true;
        }

        log("WARN", reason);
        Lost?.Invoke();
    }
}

internal static class Direct3D11Interop
{
    [DllImport("d3d11.dll", ExactSpelling = true)]
    internal static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);

    internal static void CheckError(this int hresult)
    {
        if (hresult < 0)
        {
            throw new COMException("Direct3D-Aufruf fehlgeschlagen.", hresult);
        }
    }
}
