using SnapZones.Core.Models;

namespace SnapZones.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private bool snappingEnabled;
    private bool startWithWindows;
    private OverlayScope overlayScope;
    private TriggerMode triggerMode;
    private int outerMargin;
    private int zoneGap;
    private string overlayColor;
    private double overlayOpacityPercent;

    public SettingsViewModel(AppSettings settings)
    {
        snappingEnabled = settings.SnappingEnabled;
        startWithWindows = settings.StartWithWindows;
        overlayScope = settings.OverlayScope;
        triggerMode = settings.TriggerMode;
        outerMargin = settings.OuterMargin;
        zoneGap = settings.ZoneGap;
        overlayColor = settings.OverlayColor;
        overlayOpacityPercent = settings.OverlayOpacity * 100;
    }

    public IReadOnlyList<OverlayScope> OverlayScopes { get; } = Enum.GetValues<OverlayScope>();
    public IReadOnlyList<TriggerMode> TriggerModes { get; } = Enum.GetValues<TriggerMode>();

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

    public int OuterMargin
    {
        get => outerMargin;
        set => SetProperty(ref outerMargin, Math.Clamp(value, 0, 80));
    }

    public int ZoneGap
    {
        get => zoneGap;
        set => SetProperty(ref zoneGap, Math.Clamp(value, 0, 80));
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
        activeProfileId,
        SnappingEnabled,
        StartWithWindows,
        OverlayScope,
        TriggerMode,
        OuterMargin,
        ZoneGap,
        OverlayColor,
        OverlayOpacityPercent / 100d);
}
