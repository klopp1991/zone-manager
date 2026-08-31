using ZoneManager.Core.Models;

namespace ZoneManager.Core.Geometry;

public enum MeasurementUnit
{
    Percent,
    Pixels
}

public readonly record struct ZoneMeasurement(double Value, MeasurementUnit Unit);

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
        => FromPositionAndSize(
            new ZoneMeasurement(left, unit),
            new ZoneMeasurement(top, unit),
            new ZoneMeasurement(width, unit),
            new ZoneMeasurement(height, unit),
            monitorWidth,
            monitorHeight);

    public static NormalizedRect FromPositionAndSize(
        ZoneMeasurement left,
        ZoneMeasurement top,
        ZoneMeasurement width,
        ZoneMeasurement height,
        int monitorWidth,
        int monitorHeight)
    {
        ValidateDimensions(monitorWidth, monitorHeight);
        return new NormalizedRect(
            Normalize(left, monitorWidth),
            Normalize(top, monitorHeight),
            Normalize(width, monitorWidth),
            Normalize(height, monitorHeight));
    }

    public static NormalizedRect FromMargins(
        double left,
        double top,
        double right,
        double bottom,
        MeasurementUnit unit,
        int monitorWidth,
        int monitorHeight)
        => FromMargins(
            new ZoneMeasurement(left, unit),
            new ZoneMeasurement(top, unit),
            new ZoneMeasurement(right, unit),
            new ZoneMeasurement(bottom, unit),
            monitorWidth,
            monitorHeight);

    public static NormalizedRect FromMargins(
        ZoneMeasurement left,
        ZoneMeasurement top,
        ZoneMeasurement right,
        ZoneMeasurement bottom,
        int monitorWidth,
        int monitorHeight)
    {
        ValidateDimensions(monitorWidth, monitorHeight);
        var normalizedLeft = Normalize(left, monitorWidth);
        var normalizedTop = Normalize(top, monitorHeight);
        var normalizedRight = Normalize(right, monitorWidth);
        var normalizedBottom = Normalize(bottom, monitorHeight);
        return new NormalizedRect(
            normalizedLeft,
            normalizedTop,
            1 - normalizedLeft - normalizedRight,
            1 - normalizedTop - normalizedBottom);
    }

    private static double Normalize(ZoneMeasurement measurement, int monitorPixels) =>
        measurement.Value / (measurement.Unit == MeasurementUnit.Percent ? 100d : monitorPixels);

    private static void ValidateDimensions(int monitorWidth, int monitorHeight)
    {
        if (monitorWidth <= 0 || monitorHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monitorWidth), "Die Monitorgrösse muss positiv sein.");
        }
    }
}
