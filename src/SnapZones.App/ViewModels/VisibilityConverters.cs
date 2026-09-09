using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SnapZones.App.ViewModels;

/// <summary>Wahr wird unsichtbar, falsch sichtbar: das Gegenstueck zum BooleanToVisibilityConverter.</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Sichtbar, solange eine Zahl null ist: fuer die leeren Zustaende der Listen.</summary>
public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;
        return value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Sichtbar, sobald eine Zahl groesser als null ist.</summary>
public sealed class NonZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;
        return value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Sichtbar, sobald ein Wert vorhanden ist; ein leerer Text zaehlt als nicht vorhanden.</summary>
public sealed class NotNullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;
        return value is null || value is string { Length: 0 } ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Sichtbar, solange kein Wert vorhanden ist.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;
        return value is null || value is string { Length: 0 } ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Wahr, wenn zwei gebundene Werte gleich sind – etwa die Kennung eines Tabs und die des bearbeiteten Layouts.</summary>
public sealed class EqualityConverter : IMultiValueConverter
{
    /// <summary>
    /// Liefert normalerweise <c>true</c>/<c>false</c>. Mit dem Parameter «Visibility» liefert er
    /// stattdessen <see cref="Visibility.Visible"/> beziehungsweise <see cref="Visibility.Collapsed"/> –
    /// so laesst sich derselbe Vergleich fuer eine Sichtbarkeit verwenden, ohne einen zweiten Konverter
    /// dahinterzuschalten.
    /// </summary>
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = culture;
        var equal = values.Length == 2 && values[0] is not null && Equals(values[0], values[1]);
        return parameter is string text && string.Equals(text, "Visibility", StringComparison.Ordinal)
            ? equal ? Visibility.Visible : Visibility.Collapsed
            : equal;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
