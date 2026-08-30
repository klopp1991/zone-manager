namespace SnapZones.Core.Settings;

/// <summary>
/// Everything the user interface needs to render one setting: what to call it,
/// what it does, which values it accepts and what its factory value is.
/// </summary>
/// <param name="Key">Stable identifier of the setting.</param>
/// <param name="Category">Section the setting is shown in.</param>
/// <param name="Label">Short caption next to the control.</param>
/// <param name="ShortHelp">
/// One sentence shown permanently underneath the control. Says what the setting
/// does, not how to operate it.
/// </param>
/// <param name="LongHelp">
/// The detailed explanation revealed when the user expands the help. Describes
/// what changing the value actually causes, including anything surprising.
/// </param>
/// <param name="Range">Value range for numeric settings, <c>null</c> for the others.</param>
/// <param name="Keywords">
/// Extra words the settings search should match, for terms a user is likely to
/// type that do not appear in the label or help text.
/// </param>
public sealed record SettingDescriptor(
    SettingKey Key,
    SettingCategory Category,
    string Label,
    string ShortHelp,
    string LongHelp,
    NumericSettingRange? Range = null,
    IReadOnlyList<string>? Keywords = null)
{
    public IReadOnlyList<string> Keywords { get; init; } = Keywords ?? [];

    /// <summary>True when the setting has a numeric range and can be reset to a default number.</summary>
    public bool IsNumeric => Range is not null;

    /// <summary>
    /// Matches the setting against a free-text search over label, both help
    /// texts and the extra keywords. An empty term matches everything.
    /// </summary>
    public bool Matches(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return true;
        }

        var needle = term.Trim();
        return Contains(Label, needle)
            || Contains(ShortHelp, needle)
            || Contains(LongHelp, needle)
            || Keywords.Any(keyword => Contains(keyword, needle));
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.CurrentCultureIgnoreCase);
}

/// <summary>Accepted value range of a numeric setting, in its own unit.</summary>
/// <param name="Minimum">Smallest accepted value.</param>
/// <param name="Maximum">Largest accepted value.</param>
/// <param name="Default">Factory value, used by "reset to default".</param>
/// <param name="Step">Increment of a single arrow key or slider tick.</param>
/// <param name="Unit">Unit shown next to the value, for example "px" or "%".</param>
public sealed record NumericSettingRange(
    double Minimum,
    double Maximum,
    double Default,
    double Step,
    string Unit)
{
    public double Clamp(double value) =>
        !double.IsFinite(value) ? Default : Math.Clamp(value, Minimum, Maximum);

    /// <summary>Clamps and rounds to the nearest whole step, for integer-valued settings.</summary>
    public int ClampToInt(double value) =>
        (int)Math.Round(Clamp(value), MidpointRounding.AwayFromZero);

    /// <summary>Human readable range, for example "0 – 400 px".</summary>
    public string DisplayRange => $"{Format(Minimum)} – {Format(Maximum)} {Unit}";

    /// <summary>Human readable default, for example "8 px".</summary>
    public string DisplayDefault => $"{Format(Default)} {Unit}";

    private static string Format(double value) =>
        value == Math.Floor(value)
            ? value.ToString("0")
            : value.ToString("0.#");
}
