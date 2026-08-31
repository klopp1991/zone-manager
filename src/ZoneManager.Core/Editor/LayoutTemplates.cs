using ZoneManager.Core.Models;

namespace ZoneManager.Core.Editor;

public enum LayoutTemplate
{
    TwoColumns,
    ThreeColumns,
    MainAndSide,
    Grid2x2,
    TwoRows,
    ThreeRows,
    MainAboveTwo,
    MainWithTwoSides,
    FourColumns,
    FiveColumns,
    CenteredMain,
    CenteredMainWithSidePairs
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
        LayoutTemplate.TwoRows =>
        [
            Zone("Oben", 0, 0, 1, 0.5),
            Zone("Unten", 0, 0.5, 1, 0.5)
        ],
        LayoutTemplate.ThreeRows =>
        [
            Zone("Oben", 0, 0, 1, 1d / 3d),
            Zone("Mitte", 0, 1d / 3d, 1, 1d / 3d),
            Zone("Unten", 0, 2d / 3d, 1, 1d / 3d)
        ],
        LayoutTemplate.MainAboveTwo =>
        [
            Zone("Hauptbereich", 0, 0, 1, 0.6),
            Zone("Unten links", 0, 0.6, 0.5, 0.4),
            Zone("Unten rechts", 0.5, 0.6, 0.5, 0.4)
        ],
        LayoutTemplate.MainWithTwoSides =>
        [
            Zone("Hauptbereich", 0, 0, 0.5, 1),
            Zone("Seite 1", 0.5, 0, 0.25, 1),
            Zone("Seite 2", 0.75, 0, 0.25, 1)
        ],
        LayoutTemplate.FourColumns => EqualColumns(4),
        LayoutTemplate.FiveColumns => EqualColumns(5),
        LayoutTemplate.CenteredMain =>
        [
            Zone("Links", 0, 0, 0.25, 1),
            Zone("Hauptbereich", 0.25, 0, 0.5, 1),
            Zone("Rechts", 0.75, 0, 0.25, 1)
        ],
        LayoutTemplate.CenteredMainWithSidePairs =>
        [
            Zone("Links aussen", 0, 0, 0.15, 1),
            Zone("Links innen", 0.15, 0, 0.15, 1),
            Zone("Hauptbereich", 0.3, 0, 0.4, 1),
            Zone("Rechts innen", 0.7, 0, 0.15, 1),
            Zone("Rechts aussen", 0.85, 0, 0.15, 1)
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    };

    private static IReadOnlyList<ZoneDefinition> EqualColumns(int count) =>
        Enumerable.Range(0, count)
            .Select(index => Zone(
                $"Spalte {index + 1}",
                (double)index / count,
                0,
                1d / count,
                1))
            .ToArray();

    private static ZoneDefinition Zone(string name, double x, double y, double width, double height) =>
        new(Guid.NewGuid(), name, new NormalizedRect(x, y, width, height));
}
