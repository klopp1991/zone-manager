using SnapZones.Core.Models;

namespace SnapZones.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private bool startWithWindows;
    private bool rememberWindowPositions = true;
    private bool checkForUpdatesOnStart;
    private OverlayScope overlayScope;
    private TriggerMode triggerMode;
    private ThemeMode themeMode;
    private int outerMarginLeft;
    private int outerMarginTop;
    private int outerMarginRight;
    private int outerMarginBottom;
    private int zoneGap;
    private double zoneGapPercent;
    private int magnetThresholdPixels;
    private double magnetThresholdPercent;
    private bool showZoneNames;
    private string overlayColor;
    private double overlayOpacityPercent;

    public SettingsViewModel(AppSettings settings)
    {
        startWithWindows = settings.StartWithWindows;
        rememberWindowPositions = settings.RememberWindowPositions;
        checkForUpdatesOnStart = settings.CheckForUpdatesOnStart;
        overlayScope = settings.OverlayScope;
        triggerMode = settings.TriggerMode;
        themeMode = settings.ThemeMode;
        var margins = settings.EffectiveOuterMargins;
        outerMarginLeft = margins.Left;
        outerMarginTop = margins.Top;
        outerMarginRight = margins.Right;
        outerMarginBottom = margins.Bottom;
        zoneGap = settings.ZoneGap;
        zoneGapPercent = ToPercent(zoneGap, 80);
        magnetThresholdPixels = settings.MagnetThresholdPixels;
        magnetThresholdPercent = ToPercent(magnetThresholdPixels, 40);
        showZoneNames = settings.ShowZoneNames;
        overlayColor = settings.OverlayColor;
        overlayOpacityPercent = settings.OverlayOpacity * 100;
    }

    public IReadOnlyList<OverlayScope> OverlayScopes { get; } = Enum.GetValues<OverlayScope>();
    public IReadOnlyList<TriggerMode> TriggerModes { get; } = Enum.GetValues<TriggerMode>();
    public IReadOnlyList<ThemeMode> ThemeModes { get; } = Enum.GetValues<ThemeMode>();

    public bool StartWithWindows
    {
        get => startWithWindows;
        set => SetProperty(ref startWithWindows, value);
    }

    /// <summary>
    /// Ob sich das Programm merkt, wo ein Fenster zuletzt stand, und es beim naechsten Oeffnen dorthin
    /// zuruecklegt. Ausgeschaltet bleiben bereits gemerkte Eintraege erhalten, werden aber nicht mehr
    /// angewendet; <see cref="MainViewModel.ForgetWindowPositionsRequested"/> loescht sie.
    /// </summary>
    public bool RememberWindowPositions
    {
        get => rememberWindowPositions;
        set => SetProperty(ref rememberWindowPositions, value);
    }

    /// <summary>
    /// Ob beim Start einmal nach einer neueren Veroeffentlichung gefragt wird. Voreingestellt aus: eine
    /// Abfrage geht ins Netz, und das soll das Programm nur tun, wenn es ausdruecklich gewollt ist.
    /// </summary>
    public bool CheckForUpdatesOnStart
    {
        get => checkForUpdatesOnStart;
        set => SetProperty(ref checkForUpdatesOnStart, value);
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
        set
        {
            var normalized = Math.Clamp(value, 0, 80);
            if (SetProperty(ref zoneGap, normalized))
            {
                SetProperty(ref zoneGapPercent, ToPercent(normalized, 80), nameof(ZoneGapPercent));
            }
        }
    }

    public double ZoneGapPercent
    {
        get => zoneGapPercent;
        set
        {
            var normalized = NormalizePercent(value, 0, 100);
            if (SetProperty(ref zoneGapPercent, normalized))
            {
                SetProperty(ref zoneGap, FromPercent(normalized, 80), nameof(ZoneGap));
            }
        }
    }

    public int MagnetThresholdPixels
    {
        get => magnetThresholdPixels;
        set
        {
            var normalized = Math.Clamp(value, 0, 40);
            if (SetProperty(ref magnetThresholdPixels, normalized))
            {
                SetProperty(ref magnetThresholdPercent, ToPercent(normalized, 40), nameof(MagnetThresholdPercent));
            }
        }
    }

    public double MagnetThresholdPercent
    {
        get => magnetThresholdPercent;
        set
        {
            var normalized = NormalizePercent(value, 0, 100);
            if (SetProperty(ref magnetThresholdPercent, normalized))
            {
                SetProperty(ref magnetThresholdPixels, FromPercent(normalized, 40), nameof(MagnetThresholdPixels));
            }
        }
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
        set => SetProperty(ref overlayOpacityPercent, NormalizePercent(value, 8, 75));
    }

    public AppSettings CreateSettings() => new(
        ActiveProfileId: Guid.Empty,
        SnappingEnabled: false,
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
            OuterMarginBottom),
        RememberWindowPositions: RememberWindowPositions,
        CheckForUpdatesOnStart: CheckForUpdatesOnStart);

    public void Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        StartWithWindows = settings.StartWithWindows;
        RememberWindowPositions = settings.RememberWindowPositions;
        CheckForUpdatesOnStart = settings.CheckForUpdatesOnStart;
        OverlayScope = settings.OverlayScope;
        TriggerMode = settings.TriggerMode;
        ThemeMode = settings.ThemeMode;
        var margins = settings.EffectiveOuterMargins;
        OuterMarginLeft = margins.Left;
        OuterMarginTop = margins.Top;
        OuterMarginRight = margins.Right;
        OuterMarginBottom = margins.Bottom;
        ZoneGap = settings.ZoneGap;
        MagnetThresholdPixels = settings.MagnetThresholdPixels;
        ShowZoneNames = settings.ShowZoneNames;
        OverlayColor = settings.OverlayColor;
        OverlayOpacityPercent = settings.OverlayOpacity * 100d;
    }

    private static double ToPercent(int value, int maximum) =>
        NormalizePercent((double)value / maximum * 100, 0, 100);

    private static int FromPercent(double value, int maximum) =>
        (int)Math.Round(value / 100 * maximum, MidpointRounding.AwayFromZero);

    private static double NormalizePercent(double value, double minimum, double maximum)
    {
        if (!double.IsFinite(value))
        {
            return minimum;
        }

        // Ganze Prozentschritte: Regler und Zahlenfeld zeigen denselben, ablesbaren Wert.
        var clamped = Math.Clamp(value, minimum, maximum);
        return Math.Clamp(Math.Round(clamped, MidpointRounding.AwayFromZero), minimum, maximum);
    }
}
