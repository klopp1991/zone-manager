using System.Globalization;

namespace SnapZones.Core.Updates;

/// <summary>
/// Eine Produktversion im Schema <c>YYYY.MMDD.NN</c>. Die drei Teile werden als Zahlen verglichen, nicht
/// als Text: <c>2026.0901.10</c> ist neuer als <c>2026.0901.09</c>, obwohl es alphabetisch davor stünde.
/// Führende Nullen sind erlaubt und ohne Bedeutung, denn die Anzeigeform schreibt <c>0901</c>, während
/// die Assemblyversion dieselbe Version als <c>901</c> führt.
/// </summary>
public readonly record struct ProductVersion(int Year, int MonthDay, int Sequence)
    : IComparable<ProductVersion>
{
    public static bool TryParse(string? value, out ProductVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Ein Release-Tag heisst «v2026.0901.01»; ein angehängtes Metadatensuffix wird abgeschnitten.
        var text = value.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        var suffix = text.IndexOfAny(['+', '-']);
        if (suffix >= 0)
        {
            text = text[..suffix];
        }

        var parts = text.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!TryParsePart(parts[0], out var year) ||
            !TryParsePart(parts[1], out var monthDay) ||
            !TryParsePart(parts[2], out var sequence))
        {
            return false;
        }

        version = new ProductVersion(year, monthDay, sequence);
        return true;
    }

    public int CompareTo(ProductVersion other)
    {
        var year = Year.CompareTo(other.Year);
        if (year != 0)
        {
            return year;
        }

        var monthDay = MonthDay.CompareTo(other.MonthDay);
        return monthDay != 0 ? monthDay : Sequence.CompareTo(other.Sequence);
    }

    public static bool operator <(ProductVersion left, ProductVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(ProductVersion left, ProductVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(ProductVersion left, ProductVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(ProductVersion left, ProductVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Year:0000}.{MonthDay:0000}.{Sequence:00}");

    private static bool TryParsePart(string part, out int value) =>
        int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
