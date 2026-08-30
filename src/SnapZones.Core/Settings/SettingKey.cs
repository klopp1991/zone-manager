namespace SnapZones.Core.Settings;

/// <summary>
/// Stable identifier for every user-visible setting. Used as the key into
/// <see cref="SettingsCatalog"/> so that label, help text, unit, range and
/// default value all have exactly one definition.
/// </summary>
public enum SettingKey
{
    ThemeMode,
    StartWithWindows,
    OverlayScope,
    TriggerMode,
    ShowZoneNames,
    OverlayColor,
    OverlayOpacity,
    OuterMargins,
    ZoneGap,
    MagnetThreshold
}

/// <summary>Grouping used to lay the settings page out in stable, titled sections.</summary>
public enum SettingCategory
{
    Program,
    Activation,
    OverlayAppearance,
    Spacing
}
