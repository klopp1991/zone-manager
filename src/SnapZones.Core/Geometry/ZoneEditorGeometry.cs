using SnapZones.Core.Models;

namespace SnapZones.Core.Geometry;

public enum MeasurementUnit
{
    Percent,
    Pixels
}

public sealed record ZoneEditorValues(
    double Left,
    double Top,
    double Right,
    double Bottom,
    double Width,
    double Height);

public static class ZoneEditorGeometry
{
    public static ZoneEditorValues ToValues(
        NormalizedRect bounds,
        MeasurementUnit unit,
        int monitorWidth,
        int monitorHeight)
    {
        ValidateDimensions(monitorWidth, monitorHeight);
        if (unit == MeasurementUnit.Percent)
        {
            return new ZoneEditorValues(
                bounds.X * 100,
                bounds.Y * 100,
                (1 - bounds.X - bounds.Width) * 100,
                (1 - bounds.Y - bounds.Height) * 100,
                bounds.Width * 100,
                bounds.Height * 100);
        }

        var left = (int)Math.Round(bounds.X * monitorWidth);
        var top = (int)Math.Round(bounds.Y * monitorHeight);
        var rightEdge = (int)Math.Round((bounds.X + bounds.Width) * monitorWidth);
        var bottomEdge = (int)Math.Round((bounds.Y + bounds.Height) * monitorHeight);
        return new ZoneEditorValues(
            left,
            top,
            monitorWidth - rightEdge,
            monitorHeight - bottomEdge,
            rightEdge - left,
            bottomEdge - top);
    }

    public static NormalizedRect FromPositionAndSize(
        double left,
        double top,
        double width,
        double height,
        MeasurementUnit unit,
        int monitorWidth,
        int monitorHeight)
    {
        ValidateDimensions(monitorWidth, monitorHeight);
        var horizontalDivisor = unit == MeasurementUnit.Percent ? 100d : monitorWidth;
        var verticalDivisor = unit == MeasurementUnit.Percent ? 100d : monitorHeight;
        return new NormalizedRect(
            left / horizontalDivisor,
            top / verticalDivisor,
            width / horizontalDivisor,
            height / verticalDivisor);
    }

    public static NormalizedRect FromMargins(
        double left,
        double top,
        double right,
        double bottom,
        MeasurementUnit unit,
        int monitorWidth,
        int monitorHeight)
    {
        ValidateDimensions(monitorWidth, monitorHeight);
        var horizontalDivisor = unit == MeasurementUnit.Percent ? 100d : monitorWidth;
        var verticalDivisor = unit == MeasurementUnit.Percent ? 100d : monitorHeight;
        var normalizedLeft = left / horizontalDivisor;
        var normalizedTop = top / verticalDivisor;
        var normalizedRight = right / horizontalDivisor;
        var normalizedBottom = bottom / verticalDivisor;
        return new NormalizedRect(
            normalizedLeft,
            normalizedTop,
            1 - normalizedLeft - normalizedRight,
            1 - normalizedTop - normalizedBottom);
    }

    private static void ValidateDimensions(int monitorWidth, int monitorHeight)
    {
        if (monitorWidth <= 0 || monitorHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monitorWidth), "Die Monitorgrösse muss positiv sein.");
        }
    }
}

