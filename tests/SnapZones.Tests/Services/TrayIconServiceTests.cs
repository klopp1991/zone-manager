using System.Windows.Forms;
using SnapZones.App.Services;
using SnapZones.App.Views;
using SnapZones.Tests.Support;
using SnapZones.Tests.Theme;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class TrayIconServiceTests
{
    [Fact]
    public void Update_keeps_the_existing_context_menu_alive_so_an_open_menu_stays_clickable()
    {
        WpfThemeHost.Invoke(() =>
        {
            var exitRequests = 0;
            using var service = new TrayIconService(new MainWindow(), _ => { }, () => exitRequests++);
            var menu = service.Menu;

            service.Update(ConfigurationSamples.TwoLayouts());
            service.Update(ConfigurationSamples.TwoLayouts());

            // Frueher ersetzte jede Aktualisierung das ContextMenuStrip und verwarf das alte. Fiel eine
            // Speicherung mit einem geoeffneten Menue zusammen, verschwand der gerade angeklickte Eintrag
            // und "Beenden" blieb wirkungslos.
            Assert.Same(menu, service.Menu);
            Assert.False(menu.IsDisposed);
            Assert.False(service.HasDeferredUpdate);

            var exitItem = menu.Items.Cast<ToolStripItem>().Single(item => item.Text == "Beenden");
            exitItem.PerformClick();

            Assert.Equal(1, exitRequests);
        });
    }

    [Fact]
    public void Update_rebuilds_the_entries_for_the_current_configuration()
    {
        WpfThemeHost.Invoke(() =>
        {
            using var service = new TrayIconService(new MainWindow(), _ => { }, () => { });

            service.Update(ConfigurationSamples.TwoLayouts());
            var afterFirst = service.Menu.Items.Count;
            service.Update(ConfigurationSamples.TwoLayouts());

            // Wiederholte Aktualisierungen duerfen die Eintraege nicht anhaeufen.
            Assert.Equal(afterFirst, service.Menu.Items.Count);
            Assert.Contains(service.Menu.Items.Cast<ToolStripItem>(), item => item.Text == "Editor öffnen");
            Assert.Contains(service.Menu.Items.Cast<ToolStripItem>(), item => item.Text == "Beenden");
        });
    }
}
