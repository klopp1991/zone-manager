using SnapZones.Core.Editor;
using SnapZones.Core.Models;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Editor;

/// <summary>
/// Seit dem 02.09.2026 hat der Layouteditor einen Verlauf. Vorher vernichtete ein Fehlklick auf eine
/// Vorlage ein handgebautes Layout ohne Weg zurueck.
/// </summary>
public sealed class LayoutEditorUndoTests
{
    [Fact]
    public void Undo_restores_zones_replaced_by_a_template_and_redo_reapplies_them()
    {
        var layout = ConfigurationSamples.TwoLayouts().Layouts[0];
        var session = new LayoutEditorSession(layout);
        Assert.False(session.CanUndo);

        session.ReplaceZones(LayoutTemplates.Create(LayoutTemplate.ThreeColumns));
        Assert.Equal(3, session.Zones.Count);
        Assert.True(session.CanUndo);

        Assert.True(session.Undo());
        Assert.Equal(layout.Zones, session.Zones);
        Assert.True(session.CanRedo);

        Assert.True(session.Redo());
        Assert.Equal(3, session.Zones.Count);
    }

    [Fact]
    public void A_mouse_drag_counts_as_a_single_history_entry()
    {
        var layout = ConfigurationSamples.TwoLayouts().Layouts[0];
        var session = new LayoutEditorSession(layout);
        var zoneId = layout.Zones[0].Id;

        session.BeginInteraction();
        for (var step = 1; step <= 20; step++)
        {
            session.MoveZone(zoneId, new NormalizedRect(0, 0, 0.5 - step * 0.01, 1));
        }

        session.EndInteraction();

        Assert.True(session.Undo());
        Assert.Equal(layout.Zones[0].Bounds, session.Zones[0].Bounds);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void Unchanged_updates_do_not_pollute_the_history()
    {
        var layout = ConfigurationSamples.TwoLayouts().Layouts[0];
        var session = new LayoutEditorSession(layout);

        session.UpdateZone(layout.Zones[0].Id, layout.Zones[0].Name, layout.Zones[0].Bounds);
        session.SetMainZone(null);

        Assert.False(session.CanUndo);
    }

    [Fact]
    public void A_new_change_after_undo_drops_the_redo_branch()
    {
        var layout = ConfigurationSamples.TwoLayouts().Layouts[0];
        var session = new LayoutEditorSession(layout);
        session.DeleteZone(layout.Zones[1].Id);
        session.Undo();

        session.SetMainZone(layout.Zones[0].Id);

        Assert.False(session.CanRedo);
        Assert.Equal(layout.Zones[0].Id, session.MainZoneId);
    }
}
