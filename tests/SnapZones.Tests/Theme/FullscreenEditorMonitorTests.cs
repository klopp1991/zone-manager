using System.Runtime.InteropServices;
using System.Windows.Interop;
using SnapZones.App.Controls;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Models;
using SnapZones.Core.Geometry;
using SnapZones.Core.Monitors;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Theme;

public sealed class FullscreenEditorMonitorTests
{
    [Fact]
    public void Work_area_changes_reposition_the_open_editor_and_update_its_scale()
    {
        WpfThemeHost.Invoke(() =>
        {
            var monitor = Monitor();
            var model = new MainViewModel(ConfigurationSamples.TwoLayouts(), [monitor]);
            var owner = new MainWindow();
            var editor = new FullscreenZoneEditorWindow(owner, model);
            try
            {
                editor.Show();
                var changed = monitor with { WorkArea = new MonitorWorkArea(150, 160, 900, 650) };

                model.ReplaceMonitors([changed], null);

                Assert.True(GetWindowRect(new WindowInteropHelper(editor).Handle, out var bounds));
                Assert.Equal((150, 160, 1050, 810), (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
                var canvas = Assert.IsType<LayoutCanvas>(editor.FindName("Canvas"));
                Assert.Equal(900, canvas.MonitorPixelWidth);
                Assert.Equal(650, canvas.MonitorPixelHeight);
            }
            finally
            {
                editor.Close();
                owner.Close();
            }
        });
    }

    [Fact]
    public void Disconnecting_the_edited_monitor_closes_the_editor_without_changing_the_layout()
    {
        WpfThemeHost.Invoke(() =>
        {
            var model = new MainViewModel(ConfigurationSamples.TwoLayouts(), [Monitor()]);
            var owner = new MainWindow();
            var editor = new FullscreenZoneEditorWindow(owner, model);
            var closed = false;
            editor.Closed += (_, _) => closed = true;
            try
            {
                editor.Show();
                var zones = model.SelectedLayout!.Zones.ToArray();

                model.ReplaceMonitors([], null);

                Assert.True(closed);
                Assert.Equal(zones, model.SelectedLayout!.Zones);
            }
            finally
            {
                if (!closed) editor.Close();
                owner.Close();
            }
        });
    }

    private static LiveMonitor Monitor() => new(
        new MonitorIdentity("DISPLAY-A", "DISPLAY1", "Testmonitor"),
        new MonitorWorkArea(50, 60, 800, 600), 96, 96, true);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint window, out NativeRect bounds);
}
