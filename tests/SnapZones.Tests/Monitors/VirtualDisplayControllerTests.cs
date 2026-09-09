using SnapZones.Core.Geometry;
using SnapZones.Core.Monitors;
using SnapZones.Tests.Support;
using SnapZones.Windows.Displays;
using Xunit;

namespace SnapZones.Tests.Monitors;

/// <summary>
/// Der Controller liest ohne Rechte; anhaengen, skalieren und abhaengen veraendern den Desktop und
/// laufen deshalb nur, wenn der Treiber installiert ist und <c>ZONEMANAGER_VDD_TESTS=1</c> gesetzt
/// ist — sonst wuerde jeder Testlauf den Bildschirmaufbau des Rechners umbauen.
/// </summary>
[Collection("VirtualDisplay")]
public sealed class VirtualDisplayControllerTests
{
    [Fact]
    public void Finding_the_virtual_monitor_never_throws()
    {
        var state = VirtualDisplayController.Find();

        if (state is null)
        {
            // In einer Fernsitzung gehoert der Monitor des Treibers zur getrennten Konsolensitzung:
            // das Geraet ist da, in dieser Sitzung aber nicht zu sehen.
            Assert.False(VirtualDisplayDriverService.ReadStatus().DevicePresent && !RemoteSession.IsActive);
            return;
        }

        Assert.StartsWith(@"\\.\DISPLAY", state.DeviceName);
        Assert.Equal(state.Attached, state.Mode is not null);
    }

    [Fact]
    public void Attaching_in_zone_size_copies_the_scaling_and_detaches_again()
    {
        if (Environment.GetEnvironmentVariable("ZONEMANAGER_VDD_TESTS") != "1")
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
        // Ein Modus aus der verbreiteten Liste, die die Installation immer mitbringt; die Liste zu
        // aendern braeuchte einen Geraeteneustart mit Administratorrechten.
        var mode = new VirtualDisplayMode(1920, 1080);
        if (!VirtualDisplayController.ModesAvailable(initial.DeviceName, [mode]))
        {
            return;
        }

        // Physische Pixel unabhaengig davon, ob der Testprozess DPI-bewusst ist.
        var position = VirtualDisplayPlacement.ChoosePosition(VirtualDisplayController.AttachedDisplayBounds());
        var device = initial.DeviceName;
        var attach = controller.Attach(device, mode, position);
        try
        {
            Assert.True(attach.Succeeded, attach.Message);
            var attached = VirtualDisplayController.WaitForAttached(mode, TimeSpan.FromSeconds(5));
            Assert.NotNull(attached);
            Assert.Equal(new PixelRect(position.X, position.Y, 1920, 1080), attached.Bounds);

            Assert.True(controller.TrySetScalePercent(attached.DeviceName, 150), string.Join("\n", log));
            Assert.True(VirtualDisplayController.WaitForScalePercent(attached.DeviceName, 150, TimeSpan.FromSeconds(5)), "150 % wurde nicht uebernommen");

            Assert.True(controller.TrySetScalePercent(attached.DeviceName, 100), string.Join("\n", log));
            Assert.True(VirtualDisplayController.WaitForScalePercent(attached.DeviceName, 100, TimeSpan.FromSeconds(5)), "100 % wurde nicht uebernommen");
        }
        finally
        {
            var current = VirtualDisplayController.Find();
            if (current is { Attached: true })
            {
                controller.Detach(current.DeviceName);
            }
        }

        Assert.True(VirtualDisplayController.WaitForDetached(TimeSpan.FromSeconds(5)));
    }
}
