using System.Globalization;
using System.Text;

namespace SnapZones.Core.Monitors;

/// <summary>Ein Anzeigemodus des virtuellen Monitors: Breite und Hoehe in Pixeln bei 60 Hz.</summary>
public sealed record VirtualDisplayMode(int Width, int Height)
{
    public long Area => (long)Width * Height;

    /// <summary>Ob der Modus vollstaendig in eine Flaeche dieser Groesse passt.</summary>
    public bool FitsInto(int width, int height) => Width <= width && Height <= height;

    /// <summary>Ob der Modus eine Flaeche dieser Groesse vollstaendig bedeckt.</summary>
    public bool Covers(int width, int height) => Width >= width && Height >= height;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Width}x{Height}");
}

/// <summary>
/// Die Liste der Anzeigemodi, die der Vollbildzonen-Treiber dem virtuellen Monitor anbietet. Der Treiber liest
/// sie aus <c>vdd_settings.xml</c> und nimmt hoechstens hundert Eintraege an (gemessen am 08.09.2026:
/// 100 gehen, 112 nicht). Zone Manager erzeugt die Liste aus den Groessen der virtuellen Zonen, damit
/// jede Zone einen exakt passenden Modus bekommt, und fuellt sie mit verbreiteten Groessen auf.
/// </summary>
public static class VirtualDisplayModes
{
    public const int MaximumCount = 100;
    public const int RefreshRate = 60;
    public const int MinimumWidth = 640;
    public const int MinimumHeight = 480;
    public const int MaximumWidth = 7680;
    public const int MaximumHeight = 4320;

    /// <summary>
    /// Verbreitete Groessen als Reserve: eine Zone, deren Groesse nicht in der Liste steht, bekommt
    /// den naechstkleineren dieser Modi.
    /// </summary>
    public static readonly IReadOnlyList<VirtualDisplayMode> Common =
    [
        new(1280, 720),
        new(1600, 900),
        new(1920, 1080),
        new(2560, 1440),
        new(3440, 1440),
        new(3840, 2160),
        new(1024, 768),
        new(1280, 1024),
        new(1920, 1200),
        new(2560, 1600)
    ];

    /// <summary>
    /// Rundet eine Zonengroesse auf gerade Pixelzahlen ab und begrenzt sie auf den Bereich, den der
    /// Treiber annimmt. Ungerade Breiten sind fuer Anzeigemodi unueblich; ein Pixel Unterschied ist
    /// in der Zone nicht zu sehen.
    /// </summary>
    public static VirtualDisplayMode Normalize(int width, int height) => new(
        Math.Clamp(width - (width & 1), MinimumWidth, MaximumWidth),
        Math.Clamp(height - (height & 1), MinimumHeight, MaximumHeight));

    /// <summary>
    /// Baut die Liste: zuerst die Zonengroessen in der gegebenen Reihenfolge, ohne Duplikate, danach
    /// die verbreiteten Groessen, bis die Obergrenze erreicht ist.
    /// </summary>
    public static IReadOnlyList<VirtualDisplayMode> Build(IEnumerable<VirtualDisplayMode> zoneSizes)
    {
        ArgumentNullException.ThrowIfNull(zoneSizes);
        var result = new List<VirtualDisplayMode>();
        var seen = new HashSet<VirtualDisplayMode>();
        foreach (var size in zoneSizes)
        {
            var mode = Normalize(size.Width, size.Height);
            if (result.Count < MaximumCount && seen.Add(mode))
            {
                result.Add(mode);
            }
        }

        foreach (var mode in Common)
        {
            if (result.Count < MaximumCount && seen.Add(mode))
            {
                result.Add(mode);
            }
        }

        return result;
    }

    /// <summary>
    /// Waehlt fuer eine Zonengroesse den Modus: exakt, sonst der groesste, der hineinpasst (das Bild
    /// bekommt einen schmalen Rand), sonst der kleinste, der sie bedeckt (das Bild wird beschnitten).
    /// </summary>
    public static VirtualDisplayMode? Choose(IEnumerable<VirtualDisplayMode> available, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(available);
        var wanted = Normalize(width, height);
        var modes = available as IReadOnlyCollection<VirtualDisplayMode> ?? available.ToArray();
        if (modes.Contains(wanted))
        {
            return wanted;
        }

        var fitting = modes
            .Where(mode => mode.FitsInto(wanted.Width, wanted.Height))
            .OrderByDescending(mode => mode.Area)
            .ThenByDescending(mode => mode.Width)
            .FirstOrDefault();
        if (fitting is not null)
        {
            return fitting;
        }

        return modes
            .Where(mode => mode.Covers(wanted.Width, wanted.Height))
            .OrderBy(mode => mode.Area)
            .ThenBy(mode => mode.Width)
            .FirstOrDefault();
    }

    /// <summary>
    /// Die Datei <c>vdd_settings.xml</c> fuer diese Liste. Ohne globale Bildwiederholraten: sie
    /// vervielfachen jeden Eintrag und sprengen die Obergrenze des Treibers.
    /// </summary>
    public static string ToSettingsXml(IReadOnlyList<VirtualDisplayMode> modes)
    {
        ArgumentNullException.ThrowIfNull(modes);
        if (modes.Count > MaximumCount)
        {
            throw new ArgumentException($"Der Treiber nimmt hoechstens {MaximumCount} Modi an.", nameof(modes));
        }

        var builder = new StringBuilder();
        builder.Append("<?xml version='1.0' encoding='utf-8'?>\n");
        builder.Append("<vdd_settings>\n");
        builder.Append("    <monitors><count>1</count></monitors>\n");
        builder.Append("    <gpu><friendlyname>default</friendlyname></gpu>\n");
        builder.Append("    <global></global>\n");
        builder.Append("    <resolutions>\n");
        foreach (var mode in modes)
        {
            builder.Append(CultureInfo.InvariantCulture,
                $"        <resolution><width>{mode.Width}</width><height>{mode.Height}</height><refresh_rate>{RefreshRate}</refresh_rate></resolution>\n");
        }

        builder.Append("    </resolutions>\n");
        builder.Append("    <options>\n");
        builder.Append("        <CustomEdid>false</CustomEdid><PreventSpoof>false</PreventSpoof><EdidCeaOverride>false</EdidCeaOverride>\n");
        builder.Append("        <HardwareCursor>true</HardwareCursor><SDR10bit>false</SDR10bit><HDRPlus>false</HDRPlus>\n");
        builder.Append("        <logging>false</logging><debuglogging>false</debuglogging>\n");
        builder.Append("    </options>\n");
        builder.Append("</vdd_settings>\n");
        return builder.ToString();
    }
}
