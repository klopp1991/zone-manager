using System.Windows;
using System.Windows.Interop;
using SnapZones.Core.Geometry;
using SnapZones.Core.Monitors;
using SnapZones.Windows.Windows;

namespace SnapZones.App.Overlays;

public partial class MonitorIdentificationWindow : Window
{
    public MonitorIdentificationWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => OverlayWindowNative.Configure(new WindowInteropHelper(this).Handle);
    }

    public string Label => IdentificationText.Text;

    public void ShowFor(LiveMonitor monitor, string label)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        IdentificationText.Text = label;
        if (!IsVisible)
        {
            Show();
        }

        OverlayWindowNative.Position(
            new WindowInteropHelper(this).Handle,
            new PixelRect(
                monitor.WorkArea.X,
                monitor.WorkArea.Y,
                monitor.WorkArea.Width,
                monitor.WorkArea.Height));
    }
}
