namespace SnapZones.Core.Geometry;

public enum HandoffAction
{
    /// <summary>Die Bewegung bleibt, wie sie ist.</summary>
    None,

    /// <summary>Der Zeiger hat die Zone betreten und springt auf den virtuellen Monitor.</summary>
    Enter,

    /// <summary>Der Zeiger hat den Rand des virtuellen Monitors erreicht und springt in die Zone zurueck.</summary>
    Exit,

    /// <summary>Der Zeiger kam ueber die Desktopkante auf den virtuellen Monitor und wird zurueckgesetzt.</summary>
    PushBack
}

public readonly record struct HandoffDecision(HandoffAction Action, PointInt Target)
{
    public static readonly HandoffDecision None = new(HandoffAction.None, default);
}

/// <summary>
/// Entscheidet je Mausbewegung, ob der echte Zeiger zwischen Zone und virtuellem Monitor uebergeben
/// wird. Betritt er die Zone, geht er auf die umgerechnete Stelle des virtuellen Monitors; verlaesst
/// er dort einen Rand, kommt er unmittelbar ausserhalb der Zone auf derselben Seite zurueck. Ueber die
/// Desktopkante darf er nicht auf den virtuellen Monitor gelangen; dann wird er an die Kante gesetzt.
///
/// <para>
/// Ein Rand, hinter dem in der Zone kein Monitor liegt, gibt den Zeiger nicht frei: er bliebe sonst
/// am Desktoprand haengen und traete sofort wieder ein. Nach jedem Austritt gilt eine kurze Sperre,
/// damit der Zeiger am Zonenrand nicht pendelt.
/// </para>
/// </summary>
public static class CursorHandoffPlanner
{
    public static HandoffDecision Decide(
        bool inVirtual,
        bool locked,
        PixelRect zone,
        PixelRect virtualBounds,
        MirrorPlan plan,
        IReadOnlyList<PixelRect> monitors,
        PointInt point)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        if (virtualBounds.Width <= 0 || virtualBounds.Height <= 0 || zone.Width <= 0 || zone.Height <= 0)
        {
            return HandoffDecision.None;
        }

        if (!inVirtual)
        {
            if (zone.Contains(point))
            {
                return locked ? HandoffDecision.None : new HandoffDecision(HandoffAction.Enter, MirrorGeometry.ToVirtual(plan, zone, virtualBounds, point));
            }

            if (virtualBounds.Contains(point))
            {
                return new HandoffDecision(HandoffAction.PushBack, NearestDesktopPoint(virtualBounds, monitors, point));
            }

            return HandoffDecision.None;
        }

        if (virtualBounds.Contains(point))
        {
            return HandoffDecision.None;
        }

        var inside = new PointInt(
            Math.Clamp(point.X, virtualBounds.X, virtualBounds.Right - 1),
            Math.Clamp(point.Y, virtualBounds.Y, virtualBounds.Bottom - 1));
        var zonePoint = MirrorGeometry.ToZone(plan, zone, virtualBounds, inside);
        var target = point.X < virtualBounds.X ? zonePoint with { X = zone.X - 2 }
            : point.X >= virtualBounds.Right ? zonePoint with { X = zone.Right + 1 }
            : point.Y < virtualBounds.Y ? zonePoint with { Y = zone.Y - 2 }
            : zonePoint with { Y = zone.Bottom + 1 };
        return monitors.Any(monitor => monitor.Contains(target))
            ? new HandoffDecision(HandoffAction.Exit, target)
            : new HandoffDecision(HandoffAction.PushBack, inside);
    }

    /// <summary>Der Punkt am Rand des naechsten echten Monitors, von dem aus der Zeiger hereinkam.</summary>
    private static PointInt NearestDesktopPoint(PixelRect virtualBounds, IReadOnlyList<PixelRect> monitors, PointInt point)
    {
        PointInt? best = null;
        var bestDistance = long.MaxValue;
        foreach (var monitor in monitors)
        {
            if (monitor.Width <= 0 || monitor.Height <= 0)
            {
                continue;
            }

            var candidate = new PointInt(
                Math.Clamp(point.X, monitor.X, monitor.Right - 1),
                Math.Clamp(point.Y, monitor.Y, monitor.Bottom - 1));
            var dx = (long)candidate.X - point.X;
            var dy = (long)candidate.Y - point.Y;
            var distance = dx * dx + dy * dy;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best ?? new PointInt(virtualBounds.X - 1, point.Y);
    }
}
