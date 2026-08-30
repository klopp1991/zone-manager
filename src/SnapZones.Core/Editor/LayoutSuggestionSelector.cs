using SnapZones.Core.Models;

namespace SnapZones.Core.Editor;

public sealed record LayoutSuggestionContext(
    int PixelWidth,
    int PixelHeight,
    uint DpiX,
    uint DpiY,
    double? PhysicalWidthCentimeters = null,
    double? PhysicalHeightCentimeters = null);

public sealed record LayoutSuggestion(
    LayoutTemplate Template,
    string Name,
    string Description,
    double MonitorAspectRatio,
    IReadOnlyList<ZoneDefinition> Zones)
{
    public string AccessibilityName => $"{Name}, {Zones.Count} Zonen";
}

public static class LayoutSuggestionSelector
{
    private const double MinimumZoneWidth = 360;
    private const double MinimumZoneHeight = 320;

    public static IReadOnlyList<LayoutSuggestion> Recommend(LayoutSuggestionContext context)
    {
        Validate(context);

        var effectiveWidth = context.PixelWidth * 96d / Math.Max(96u, context.DpiX);
        var effectiveHeight = context.PixelHeight * 96d / Math.Max(96u, context.DpiY);
        var aspectRatio = (double)context.PixelWidth / context.PixelHeight;
        var maximumZoneCount = MaximumZoneCount(context, effectiveWidth, effectiveHeight);

        var suggestions = OrderedTemplates(aspectRatio)
            .Select(template => CreateSuggestion(template, aspectRatio))
            .Where(suggestion => suggestion.Zones.Count <= maximumZoneCount)
            .Where(suggestion => Fits(suggestion.Zones, effectiveWidth, effectiveHeight))
            .Take(5)
            .ToArray();

        return suggestions.Length > 0
            ? suggestions
            : [CreateSuggestion(aspectRatio < 0.9 ? LayoutTemplate.TwoRows : LayoutTemplate.TwoColumns, aspectRatio)];
    }

    private static IEnumerable<LayoutTemplate> OrderedTemplates(double aspectRatio) => aspectRatio switch
    {
        < 0.9 =>
        [
            LayoutTemplate.TwoRows,
            LayoutTemplate.MainAboveTwo,
            LayoutTemplate.ThreeRows
        ],
        < 1.45 =>
        [
            LayoutTemplate.TwoColumns,
            LayoutTemplate.MainAndSide,
            LayoutTemplate.Grid2x2,
            LayoutTemplate.MainAboveTwo
        ],
        < 2.05 =>
        [
            LayoutTemplate.TwoColumns,
            LayoutTemplate.ThreeColumns,
            LayoutTemplate.MainWithTwoSides,
            LayoutTemplate.Grid2x2,
            LayoutTemplate.MainAndSide
        ],
        < 3.2 =>
        [
            LayoutTemplate.ThreeColumns,
            LayoutTemplate.CenteredMain,
            LayoutTemplate.FourColumns,
            LayoutTemplate.MainWithTwoSides,
            LayoutTemplate.TwoColumns
        ],
        _ =>
        [
            LayoutTemplate.FourColumns,
            LayoutTemplate.CenteredMainWithSidePairs,
            LayoutTemplate.FiveColumns,
            LayoutTemplate.CenteredMain,
            LayoutTemplate.ThreeColumns,
            LayoutTemplate.TwoColumns
        ]
    };

    private static LayoutSuggestion CreateSuggestion(LayoutTemplate template, double aspectRatio)
    {
        var (name, description) = template switch
        {
            LayoutTemplate.TwoColumns => ("Zwei Spalten", "2 gleich breite Bereiche"),
            LayoutTemplate.ThreeColumns => ("Drei Spalten", "3 gleich breite Bereiche"),
            LayoutTemplate.MainAndSide => ("Haupt + Seite", "Breit links, Seite rechts"),
            LayoutTemplate.Grid2x2 => ("Zweimal zwei", "4 Bereiche im Raster"),
            LayoutTemplate.TwoRows => ("Zwei Reihen", "2 gleich hohe Bereiche"),
            LayoutTemplate.ThreeRows => ("Drei Reihen", "3 gleich hohe Bereiche"),
            LayoutTemplate.MainAboveTwo => ("Haupt + zwei unten", "Breit oben, 2 unten"),
            LayoutTemplate.MainWithTwoSides => ("Haupt + zwei Seiten", "Breite Mitte, 2 Seiten"),
            LayoutTemplate.FourColumns => ("Vier Spalten", "4 gleich breite Bereiche"),
            LayoutTemplate.FiveColumns => ("Fünf Spalten", "5 gleich breite Bereiche"),
            LayoutTemplate.CenteredMain => ("Hauptbereich mittig", "Breite Mitte, 2 Seiten"),
            LayoutTemplate.CenteredMainWithSidePairs => ("Mitte + Seitenpaare", "Breite Mitte, 4 Seiten"),
            _ => throw new ArgumentOutOfRangeException(nameof(template))
        };

        return new LayoutSuggestion(
            template,
            name,
            description,
            aspectRatio,
            LayoutTemplates.Create(template));
    }

    private static bool Fits(
        IReadOnlyList<ZoneDefinition> zones,
        double effectiveWidth,
        double effectiveHeight) =>
        zones.All(zone =>
            zone.Bounds.Width * effectiveWidth >= MinimumZoneWidth &&
            zone.Bounds.Height * effectiveHeight >= MinimumZoneHeight);

    private static int MaximumZoneCount(
        LayoutSuggestionContext context,
        double effectiveWidth,
        double effectiveHeight)
    {
        var effectiveArea = effectiveWidth * effectiveHeight;
        var maximum = effectiveArea < 1_600_000 ? 2 : effectiveArea < 3_000_000 ? 4 : 5;

        if (context.PhysicalWidthCentimeters is > 0 && context.PhysicalHeightCentimeters is > 0)
        {
            var diagonalInches = Math.Sqrt(
                Math.Pow(context.PhysicalWidthCentimeters.Value, 2) +
                Math.Pow(context.PhysicalHeightCentimeters.Value, 2)) / 2.54;
            var physicalMaximum = diagonalInches switch
            {
                < 17 => 2,
                < 20 => 3,
                < 32 => 4,
                _ => 5
            };
            maximum = Math.Min(maximum, physicalMaximum);
        }

        return maximum;
    }

    private static void Validate(LayoutSuggestionContext context)
    {
        if (context.PixelWidth <= 0 || context.PixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(context), "Die Monitorauflösung muss positiv sein.");
        }
    }
}
