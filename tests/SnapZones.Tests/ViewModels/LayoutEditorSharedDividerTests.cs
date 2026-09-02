using SnapZones.App.ViewModels;
using SnapZones.Core.Models;
using Xunit;

namespace SnapZones.Tests.ViewModels;

public sealed class LayoutEditorSharedDividerTests
{
    [Fact]
    public void MoveOrResizeZones_updates_both_zones_and_notifies_once()
    {
        var monitor = new MonitorIdentity("DISPLAY-A", "\\\\.\\DISPLAY1", "Hauptmonitor");
        var left = new ZoneDefinition(Guid.NewGuid(), "Links", new NormalizedRect(0, 0, 0.5, 1));
        var right = new ZoneDefinition(Guid.NewGuid(), "Rechts", new NormalizedRect(0.5, 0, 0.5, 1));
        var editor = new LayoutEditorViewModel(new MonitorLayout(monitor, 3440, 1440, [left, right]));
        var notificationCount = 0;
        editor.ConfigurationChanged += () => notificationCount++;

        editor.MoveOrResizeZones(
            left.Id,
            new Dictionary<Guid, NormalizedRect>
            {
                [left.Id] = new NormalizedRect(0, 0, 0.4, 1),
                [right.Id] = new NormalizedRect(0.4, 0, 0.6, 1)
            });

        Assert.Equal(new NormalizedRect(0, 0, 0.4, 1), editor.Zones[0].Bounds);
        Assert.Equal(new NormalizedRect(0.4, 0, 0.6, 1), editor.Zones[1].Bounds);
        Assert.Equal(left.Id, editor.SelectedZone?.Id);
        Assert.Equal(1, notificationCount);
    }
}
