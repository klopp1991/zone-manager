namespace SnapZones.Core.Monitors;

/// <summary>
/// Die Skalierungsstufen, die Windows je Monitor anbietet. Der virtuelle Monitor bekommt die Stufe
/// des Monitors, auf dem seine Zone liegt; sonst waere jede Oberflaeche in der Zone kleiner als daneben.
/// </summary>
public static class DisplayScaling
{
    public const uint BaseDpi = 96;

    /// <summary>Die Stufen in der Reihenfolge, in der Windows sie zaehlt.</summary>
    public static readonly IReadOnlyList<int> Percentages = [100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500];

    /// <summary>Die Stufe, die einem DPI-Wert am naechsten kommt (144 dpi sind 150 %).</summary>
    public static int NearestPercent(uint dpi)
    {
        var exact = dpi * 100.0 / BaseDpi;
        var best = Percentages[0];
        foreach (var percent in Percentages)
        {
            if (Math.Abs(percent - exact) < Math.Abs(best - exact))
            {
                best = percent;
            }
        }

        return best;
    }

    /// <summary>Die Nummer der Stufe, oder -1 fuer einen Wert, den Windows nicht kennt.</summary>
    public static int IndexOf(int percent)
    {
        for (var index = 0; index < Percentages.Count; index++)
        {
            if (Percentages[index] == percent)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Der DPI-Wert einer Stufe (150 % sind 144 dpi).</summary>
    public static uint DpiFor(int percent) => (uint)Math.Round(BaseDpi * percent / 100.0);
}
