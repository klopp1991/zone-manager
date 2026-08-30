using SnapZones.Core.Models;

namespace SnapZones.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private bool snappingEnabled;
    private bool startWithWindows;
    private OverlayScope overlayScope;
    private TriggerMode triggerMode;
    private ThemeMode themeMode;
    private int outerMarginLeft;
    private int outerMarginTop;
    private int outerMarginRight;
    private int outerMarginBottom;
    private int zoneGap;
    private int magnetThresholdPixels;
    private bool showZoneNames;
    private string overlayColor;
    private double overlayOpacityPercent;

    public SettingsViewModel(AppSettings settings)
    {
        snappingEnabled = settings.SnappingEnabled;
        startWithWindows = settings.StartWithWindows;
        overlayScope = settings.OverlayScope;
        triggerMode = settings.TriggerMode;
        themeMode = settings.ThemeMode;
        var margins = settings.EffectiveOuterMargins;
        outerMarginLeft = margins.Left;
        outerMarginTop = margins.Top;
        outerMarginRight = margins.Right;
        outerMarginBottom = margins.Bottom;
        zoneGap = settings.ZoneGap;
        magnetThresholdPixels = settings.MagnetThresholdPixels;
        showZoneNames = settings.ShowZoneNames;
        overlayColor = settings.OverlayColor;
        overlayOpacityPercent = settings.OverlayOpacity * 100;
    }

    public IReadOnlyList<OverlayScope> OverlayScopes { get; } = Enum.GetValues<OverlayScope>();
    public IReadOnlyList<TriggerMode> TriggerModes { get; } = Enum.GetValues<TriggerMode>();
    public IReadOnlyList<ThemeMode> ThemeModes { get; } = Enum.GetValues<ThemeMode>();

    public bool SnappingEnabled
    {
        get => snappingEnabled;
        set => SetProperty(ref snappingEnabled, value);
    }

    public bool StartWithWindows
    {
        get => startWithWindows;
        set => SetProperty(ref startWithWindows, value);
    }

    public OverlayScope OverlayScope
    {
        get => overlayScope;
        set => SetProperty(ref overlayScope, value);
    }

    public TriggerMode TriggerMode
    {
        get => triggerMode;
        set => SetProperty(ref triggerMode, value);
    }

    public ThemeMode ThemeMode
    {
        get => themeMode;
        set => SetProperty(ref themeMode, value);
    }

    public int OuterMargin
    {
        get => outerMarginLeft;
        set
        {
            OuterMarginLeft = value;
            OuterMarginTop = value;
            OuterMarginRight = value;
            OuterMarginBottom = value;
        }
    }

    public int OuterMarginLeft
    {
        get => outerMarginLeft;
        set => SetProperty(ref outerMarginLeft, Math.Clamp(value, 0, 400));
    }

    public int OuterMarginTop
    {
        get => outerMarginTop;
        set => SetProperty(ref outerMarginTop, Math.Clamp(value, 0, 400));
    }

    public int OuterMarginRight
    {
        get => outerMarginRight;
        set => SetProperty(ref outerMarginRight, Math.Clamp(value, 0, 400));
    }

    public int OuterMarginBottom
    {
        get => outerMarginBottom;
        set => SetProperty(ref outerMarginBottom, Math.Clamp(value, 0, 400));
    }

    public int ZoneGap
    {
        get => zoneGap;
        set => SetProperty(ref zoneGap, Math.Clamp(value, 0, 80));
    }

    public int MagnetThresholdPixels
    {
        get => magnetThresholdPixels;
        set => SetProperty(ref magnetThresholdPixels, Math.Clamp(value, 0, 40));
    }

    public bool ShowZoneNames
    {
        get => showZoneNames;
        set => SetProperty(ref showZoneNames, value);
    }

    public string OverlayColor
    {
        get => overlayColor;
        set => SetProperty(ref overlayColor, value);
    }

    public double OverlayOpacityPercent
    {
        get => overlayOpacityPercent;
        set => SetProperty(ref overlayOpacityPercent, Math.Clamp(value, 8, 75));
    }

    public AppSettings CreateSettings(Guid activeProfileId) => new(
        ActiveProfileId: activeProfileId,
        SnappingEnabled: SnappingEnabled,
        StartWithWindows: StartWithWindows,
        OverlayScope: OverlayScope,
        TriggerMode: TriggerMode,
        OuterMargin: OuterMarginLeft,
        ZoneGap: ZoneGap,
        OverlayColor: OverlayColor,
        OverlayOpacity: OverlayOpacityPercent / 100d,
        ThemeMode: ThemeMode,
        MagnetThresholdPixels: MagnetThresholdPixels,
        ShowZoneNames: ShowZoneNames,
        OuterMargins: new EdgeInsets(
            OuterMarginLeft,
            OuterMarginTop,
            OuterMarginRight,
            OuterMarginBottom));
}
