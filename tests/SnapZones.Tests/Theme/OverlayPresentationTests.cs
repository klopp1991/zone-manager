using System.Windows;
using System.Windows.Controls;
using SnapZones.App.Overlays;
using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using Xunit;

namespace SnapZones.Tests.Theme;

public sealed class OverlayPresentationTests
{
    [Fact]
    public void Overlay_keeps_a_quiet_visual_margin_without_changing_zone_geometry()
    {
        WpfThemeHost.Invoke(() =>
        {
            var firstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var secondId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var monitor = new LiveMonitor(
                new MonitorIdentity("A", "DISPLAY1", "Testmonitor"),
                new MonitorWorkArea(-10000, -10000, 1000, 600),
                96,
                96,
                true);
            var target = new DragMonitorTarget(monitor,
            [
                new ZoneDefinition(firstId, "Links", new NormalizedRect(0, 0, 0.5, 1)),
                new ZoneDefinition(secondId, "Rechts", new NormalizedRect(0.5, 0, 0.5, 1))
            ]);
            var window = new MonitorOverlayWindow();

            try
            {
                window.ShowFor(target, new LayoutMetrics(0, 0), "#707070", 0.12, true);
                var canvas = Assert.IsType<Canvas>(window.Content);
                var zones = canvas.Children.OfType<Border>().ToArray();

                Assert.Equal(2, zones.Length);
                Assert.Equal(8d, Canvas.GetLeft(zones[0]));
                Assert.Equal(484d, zones[0].Width);
                Assert.Equal(508d, Canvas.GetLeft(zones[1]));
                Assert.Equal(484d, zones[1].Width);
                Assert.Equal(8d, Canvas.GetTop(zones[0]));
                Assert.Equal(584d, zones[0].Height);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
