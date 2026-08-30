using SnapZones.Core.Models;

namespace SnapZones.Core.Editor;

public enum LayoutTemplate
{
    TwoColumns,
    ThreeColumns,
    MainAndSide,
    Grid2x2
}

public static class LayoutTemplates
{
    public static IReadOnlyList<ZoneDefinition> Create(LayoutTemplate template) => template switch
    {
        LayoutTemplate.TwoColumns =>
        [
            Zone("Links", 0, 0, 0.5, 1),
            Zone("Rechts", 0.5, 0, 0.5, 1)
        ],
        LayoutTemplate.ThreeColumns =>
        [
            Zone("Links", 0, 0, 1d / 3d, 1),
            Zone("Mitte", 1d / 3d, 0, 1d / 3d, 1),
            Zone("Rechts", 2d / 3d, 0, 1d / 3d, 1)
        ],
        LayoutTemplate.MainAndSide =>
        [
            Zone("Hauptbereich", 0, 0, 0.7, 1),
            Zone("Seitenbereich", 0.7, 0, 0.3, 1)
        ],
        LayoutTemplate.Grid2x2 =>
        [
            Zone("Oben links", 0, 0, 0.5, 0.5),
            Zone("Oben rechts", 0.5, 0, 0.5, 0.5),
            Zone("Unten links", 0, 0.5, 0.5, 0.5),
            Zone("Unten rechts", 0.5, 0.5, 0.5, 0.5)
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    };

    private static ZoneDefinition Zone(string name, double x, double y, double width, double height) =>
        new(Guid.NewGuid(), name, new NormalizedRect(x, y, width, height));
}
