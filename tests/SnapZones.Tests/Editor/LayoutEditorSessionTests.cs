using SnapZones.Core.Editor;
using SnapZones.Core.Models;
using Xunit;

namespace SnapZones.Tests.Editor;

public sealed class LayoutEditorSessionTests
{
    [Fact]
    public void Reset_restores_saved_layout_after_multiple_draft_edits()
    {
        var saved = SavedMonitorLayout();
        var session = new LayoutEditorSession(saved);
        var added = session.AddZone("Neu", new NormalizedRect(0.5, 0, 0.5, 1));
        session.MoveZone(added.Id, new NormalizedRect(0.4, 0, 0.6, 1));

        session.Reset();

        Assert.Equal(saved.Zones, session.Zones);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void CreateSnapshot_contains_current_valid_draft()
    {
        var session = new LayoutEditorSession(SavedMonitorLayout());
        session.ResizeZone(session.Zones[0].Id, new NormalizedRect(0, 0, 0.5, 1));
        session.AddZone("Rechts", new NormalizedRect(0.5, 0, 0.5, 1));

        var snapshot = session.CreateSnapshot();

        Assert.Equal(2, snapshot.Zones.Count);
        Assert.True(session.Validation.IsValid);
        Assert.True(session.IsDirty);
    }

    private static MonitorLayout SavedMonitorLayout()
    {
        var monitor = new MonitorIdentity("DISPLAY-A", "\\\\.\\DISPLAY1", "Hauptmonitor");
        var zone = new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full);
        return new MonitorLayout(monitor, 3440, 1440, [zone]);
    }
}
