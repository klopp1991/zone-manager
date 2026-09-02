namespace SnapZones.Core.Models;

/// <summary>
/// Wie das Overlay beim Ziehen aussieht. Bis zum 02.09.2026 waren Rahmenbreite, Eckenradius,
/// Schriftgroesse und Hervorhebung fest im Code; erfahrene Anwender wollen sie an Monitor und Geschmack
/// anpassen.
/// </summary>
public sealed record OverlayStyle(
    OverlayLabelStyle LabelStyle,
    int BorderThickness,
    int CornerRadius,
    int LabelFontSize,
    string HighlightColor,
    double HighlightOpacity)
{
    public static OverlayStyle Default { get; } = new(OverlayLabelStyle.NumberAndName, 1, 4, 13, "#707070", 0.36);

    public static OverlayStyle From(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new OverlayStyle(
            settings.OverlayLabelStyle,
            Math.Clamp(settings.OverlayBorderThickness, 1, 6),
            Math.Clamp(settings.OverlayCornerRadius, 0, 24),
            Math.Clamp(settings.OverlayLabelFontSize, 10, 24),
            settings.EffectiveHighlightColor,
            Math.Clamp(settings.HighlightOpacity, 0.10, 0.90));
    }

    /// <summary>Die Beschriftung einer Zone nach dem gewaehlten Stil.</summary>
    public string Label(int number, string name) => LabelStyle switch
    {
        OverlayLabelStyle.NumberOnly => number.ToString(),
        OverlayLabelStyle.NameOnly => name,
        _ => $"{number} · {name}"
    };
}
