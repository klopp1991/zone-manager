using SnapZones.Core.Geometry;

namespace SnapZones.Core.PartMonitors;

/// <summary>
/// Was nach dem Setzen eines Fensters tatsaechlich herausgekommen ist. Bis zum 02.09.2026 galt ein
/// Fenster als platziert, sobald Windows den Aufruf angenommen hatte; Fenster mit Mindestgroesse landeten
/// kleiner oder versetzt, und das Programm meldete trotzdem Erfolg.
/// </summary>
/// <param name="Succeeded">Ob das Fenster innerhalb der Toleranz dort liegt, wo es hin sollte.</param>
/// <param name="ActualBounds">Das gemessene Fensterrechteck nach dem Setzen, sofern messbar.</param>
/// <param name="Rejection">Warum es nicht geklappt hat, im Klartext; null bei Erfolg.</param>
public sealed record PlacementOutcome(bool Succeeded, PixelRect? ActualBounds, string? Rejection)
{
    /// <summary>Wie weit die Kanten abweichen duerfen, damit eine Platzierung als gelungen gilt.</summary>
    public const int TolerancePixels = 2;

    public static PlacementOutcome Success(PixelRect? actualBounds = null) => new(true, actualBounds, null);

    public static PlacementOutcome Rejected(string reason, PixelRect? actualBounds = null) =>
        new(false, actualBounds, reason);

    /// <summary>Ob das Fenster ueberhaupt bewegt wurde; wichtig fuer die Frage nach Administratorrechten.</summary>
    public bool WindowMoved => ActualBounds is not null;
}
