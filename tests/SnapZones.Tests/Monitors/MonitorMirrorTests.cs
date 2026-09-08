using SnapZones.App.Overlays;
using SnapZones.Core.Geometry;
using SnapZones.Core.Monitors;
using SnapZones.Tests.Theme;
using SnapZones.Windows.Capture;
using SnapZones.Windows.Displays;
using Xunit;

namespace SnapZones.Tests.Monitors;

/// <summary>
/// Der Spiegel muss ohne Rahmen starten und den Verlust der Aufnahme melden, sobald der virtuelle
/// Monitor abgehaengt wird. Der Lauf baut den Desktop um und ist deshalb an
/// <c>ZONEMANAGER_VDD_TESTS=1</c> gebunden.
/// </summary>
[Collection("VirtualDisplay")]
public sealed class MonitorMirrorTests
{
    [Fact]
    public void Capture_support_is_reported_without_throwing()
    {
        Assert.True(MonitorMirror.IsSupported() || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000));
    }

    [Fact]
    public async Task The_mirror_starts_borderless_and_reports_the_loss_when_the_monitor_detaches()
    {
        if (Environment.GetEnvironmentVariable("ZONEMANAGER_VDD_TESTS") != "1" || !MonitorMirror.IsSupported())
        {
            return;
        }

        var initial = VirtualDisplayController.Find();
        if (initial is null || initial.Attached)
        {
            return;
        }

        var log = new List<string>();
        var controller = new VirtualDisplayController((level, message) => log.Add($"{level} {message}"));
        var mode = new VirtualDisplayMode(1920, 1080);
        if (!VirtualDisplayController.ModesAvailable(initial.DeviceName, [mode]))
        {
            return;
        }

        var position = VirtualDisplayPlacement.ChoosePosition(VirtualDisplayController.AttachedDisplayBounds());
        Assert.True(controller.Attach(initial.DeviceName, mode, position).Succeeded, string.Join("\n", log));

        MirrorWindow? window = null;
        MonitorMirror? mirror = null;
        var lost = new ManualResetEventSlim(false);
        try
        {
            var attached = VirtualDisplayController.WaitForAttached(mode, TimeSpan.FromSeconds(5));
            Assert.NotNull(attached);
            var monitor = VirtualDisplayController.WaitForStableMonitorHandle(attached.DeviceName, TimeSpan.FromMilliseconds(750), TimeSpan.FromSeconds(5));
            Assert.NotEqual(0, monitor);
            Assert.True(await MonitorMirror.RequestBorderlessAsync(), "Aufnahme ohne Rahmen wurde nicht erlaubt");

            // Ein Fenster auf dem virtuellen Monitor, dessen Farbe alle 40 ms wechselt: ohne bewegten
            // Inhalt liefert die Aufnahme kein einziges Bild, und der Spiegel liesse sich nicht pruefen.
            System.Windows.Forms.Form? source = null;
            System.Windows.Forms.Timer? animation = null;
            WpfThemeHost.Invoke(() =>
            {
                source = new System.Windows.Forms.Form
                {
                    FormBorderStyle = System.Windows.Forms.FormBorderStyle.None,
                    StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                    ShowInTaskbar = false,
                    BackColor = System.Drawing.Color.Red
                };
                source.SetBounds(attached.Bounds.X + 100, attached.Bounds.Y + 100, 800, 600);
                source.Show();
                var step = 0;
                animation = new System.Windows.Forms.Timer { Interval = 40 };
                animation.Tick += (_, _) => source.BackColor = ++step % 2 == 0 ? System.Drawing.Color.Red : System.Drawing.Color.Blue;
                animation.Start();

                window = new MirrorWindow();
                window.ShowAt(new PixelRect(position.X - 700, position.Y + 100, 640, 360));
                mirror = new MonitorMirror(window.Handle, 640, 360, () => VirtualDisplayController.FindMonitorHandle(attached.DeviceName), (level, message) => log.Add($"{level} {message}"));
                mirror.Lost += lost.Set;
                mirror.Start();
            });

            Thread.Sleep(1500);
            Assert.False(lost.IsSet, string.Join("\n", log));
            Assert.Contains(log, line => line.Contains("Spiegel gestartet", StringComparison.Ordinal));
            Assert.True(mirror!.TotalFrames > 5, $"Nur {mirror.TotalFrames} Bilder in 1,5 s:\n{string.Join("\n", log)}");
            WpfThemeHost.Invoke(() =>
            {
                animation?.Stop();
                source?.Close();
            });

            Assert.True(controller.Detach(attached.DeviceName).Succeeded, string.Join("\n", log));
            Assert.True(lost.Wait(TimeSpan.FromSeconds(8)), "Der Spiegel hat den Verlust des Monitors nicht gemeldet:\n" + string.Join("\n", log));
        }
        finally
        {
            WpfThemeHost.Invoke(() =>
            {
                mirror?.Dispose();
                window?.Close();
            });
            var current = VirtualDisplayController.Find();
            if (current is { Attached: true })
            {
                controller.Detach(current.DeviceName);
            }
        }
    }
}
