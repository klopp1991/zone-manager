using SnapZones.Core.Models;

namespace SnapZones.App.ViewModels;

/// <summary>
/// Die Einstellungen der Oberflaeche. Grundwerte und Feinabstimmung liegen im selben Modell; welche Karten
/// sichtbar sind, entscheidet <see cref="ShowAdvancedSettings"/>. Jeder Wert wird beim Setzen auf seinen
/// gueltigen Bereich begrenzt; die Grenzen stehen in den Hilfetexten der Oberflaeche.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private bool startWithWindows;
    private bool rememberWindowPositions = true;
    private bool checkForUpdatesOnStart;
    private ElevationMode elevationMode = ElevationMode.WhenNeeded;
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
    private bool showAdvancedSettings;
    private int overlayShowDelayMilliseconds;
    private bool activateWindowAfterSnap;
    private bool restoreSizeWhenLeavingZone;
    private FixedSizeWindowPlacement fixedSizeWindowPlacement;
    private int placementTolerancePixels;
    private int snappedTolerancePixels;
    private bool catchNewWindowsInMainZone;
    private bool preferRememberedZone;
    private bool restoreMaximizedWindows;
    private int rememberedWindowLimit;
    private int newWindowSettleDelayMilliseconds;
    private int ruleRetryDelayMilliseconds;
    private bool zoneHotkeysEnabled;
    private ZoneHotkeyModifiers zoneHotkeyModifiers;
    private OverlayLabelStyle overlayLabelStyle;
    private int overlayBorderThickness;
    private int overlayCornerRadius;
    private int overlayLabelFontSize;
    private string highlightColor = string.Empty;
    private double highlightOpacityPercent;
    private int moveHookEventLimit;
    private int dragWatchdogSeconds;

    public SettingsViewModel(AppSettings settings)
    {
        overlayColor = settings.OverlayColor;
        Apply(settings);
    }

    public IReadOnlyList<OverlayScope> OverlayScopes { get; } = Enum.GetValues<OverlayScope>();
    public IReadOnlyList<TriggerMode> TriggerModes { get; } = Enum.GetValues<TriggerMode>();
    public IReadOnlyList<ThemeMode> ThemeModes { get; } = Enum.GetValues<ThemeMode>();
    public IReadOnlyList<ElevationMode> ElevationModes { get; } = Enum.GetValues<ElevationMode>();
    public IReadOnlyList<FixedSizeWindowPlacement> FixedSizeWindowPlacements { get; } = Enum.GetValues<FixedSizeWindowPlacement>();
    public IReadOnlyList<ZoneHotkeyModifiers> ZoneHotkeyModifierChoices { get; } = Enum.GetValues<ZoneHotkeyModifiers>();
    public IReadOnlyList<OverlayLabelStyle> OverlayLabelStyles { get; } = Enum.GetValues<OverlayLabelStyle>();

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

    /// <summary>
    /// Wann sich das Programm Administratorrechte holt. Die Umstellung wirkt erst beim naechsten Start:
    /// ein laufender Prozess kann seine Rechte weder ablegen noch nachtraeglich erweitern.
    /// </summary>
    public ElevationMode ElevationMode
    {
        get => elevationMode;
        set => SetProperty(ref elevationMode, value);
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

    /// <summary>Blendet die Karten der Feinabstimmung ein. Wird gespeichert wie jede andere Einstellung.</summary>
    public bool ShowAdvancedSettings
    {
        get => showAdvancedSettings;
        set => SetProperty(ref showAdvancedSettings, value);
    }

    /// <summary>Wie lange nach dem Anfassen die Zonen erst erscheinen; 0 bis 1000 ms.</summary>
    public int OverlayShowDelayMilliseconds
    {
        get => overlayShowDelayMilliseconds;
        set => SetProperty(ref overlayShowDelayMilliseconds, Math.Clamp(value, 0, 1000));
    }

    public bool ActivateWindowAfterSnap
    {
        get => activateWindowAfterSnap;
        set => SetProperty(ref activateWindowAfterSnap, value);
    }

    public bool RestoreSizeWhenLeavingZone
    {
        get => restoreSizeWhenLeavingZone;
        set => SetProperty(ref restoreSizeWhenLeavingZone, value);
    }

    public FixedSizeWindowPlacement FixedSizeWindowPlacement
    {
        get => fixedSizeWindowPlacement;
        set => SetProperty(ref fixedSizeWindowPlacement, value);
    }

    /// <summary>Wie viele Pixel das gesetzte Fenster von der Zielflaeche abweichen darf; 0 bis 10.</summary>
    public int PlacementTolerancePixels
    {
        get => placementTolerancePixels;
        set => SetProperty(ref placementTolerancePixels, Math.Clamp(value, 0, 10));
    }

    /// <summary>Ab welcher Naehe zu den Zonenkanten ein Fenster als eingerastet gilt; 8 bis 80 Pixel.</summary>
    public int SnappedTolerancePixels
    {
        get => snappedTolerancePixels;
        set => SetProperty(ref snappedTolerancePixels, Math.Clamp(value, 8, 80));
    }

    public bool CatchNewWindowsInMainZone
    {
        get => catchNewWindowsInMainZone;
        set => SetProperty(ref catchNewWindowsInMainZone, value);
    }

    public bool PreferRememberedZone
    {
        get => preferRememberedZone;
        set => SetProperty(ref preferRememberedZone, value);
    }

    public bool RestoreMaximizedWindows
    {
        get => restoreMaximizedWindows;
        set => SetProperty(ref restoreMaximizedWindows, value);
    }

    /// <summary>Wie viele Fensterpositionen der Katalog hoechstens haelt; 50 bis 2000.</summary>
    public int RememberedWindowLimit
    {
        get => rememberedWindowLimit;
        set => SetProperty(ref rememberedWindowLimit, Math.Clamp(value, 50, 2000));
    }

    /// <summary>Wartezeit, bevor ein neues Fenster beurteilt wird; 0 bis 2000 ms.</summary>
    public int NewWindowSettleDelayMilliseconds
    {
        get => newWindowSettleDelayMilliseconds;
        set => SetProperty(ref newWindowSettleDelayMilliseconds, Math.Clamp(value, 0, 2000));
    }

    /// <summary>Abstand zwischen zwei Versuchen einer Regel; 50 bis 2000 ms.</summary>
    public int RuleRetryDelayMilliseconds
    {
        get => ruleRetryDelayMilliseconds;
        set => SetProperty(ref ruleRetryDelayMilliseconds, Math.Clamp(value, 50, 2000));
    }

    public bool ZoneHotkeysEnabled
    {
        get => zoneHotkeysEnabled;
        set => SetProperty(ref zoneHotkeysEnabled, value);
    }

    public ZoneHotkeyModifiers ZoneHotkeyModifiers
    {
        get => zoneHotkeyModifiers;
        set
        {
            if (SetProperty(ref zoneHotkeyModifiers, value))
            {
                OnPropertyChanged(nameof(ZoneHotkeyModifierLabel));
                OnPropertyChanged(nameof(ZoneHotkeyModifiersBlockAltGr));
            }
        }
    }

    /// <summary>Die Zusatztasten in Worten, fuer die Tabelle der Tastenkuerzel.</summary>
    public string ZoneHotkeyModifierLabel => DescribeModifiers(zoneHotkeyModifiers);

    /// <summary>
    /// Ob die gewaehlten Zusatztasten die AltGr-Zeichen der Zifferntasten blockieren. Windows liefert
    /// AltGr intern als Strg + Alt; ein globales Kuerzel mit diesen Tasten verschluckt daher @, # und |
    /// auf einer Schweizer und {, [ und ] auf einer deutschen Tastatur.
    /// </summary>
    public bool ZoneHotkeyModifiersBlockAltGr => zoneHotkeyModifiers == ZoneHotkeyModifiers.ControlAlt;

    public static string DescribeModifiers(ZoneHotkeyModifiers modifiers) => modifiers switch
    {
        ZoneHotkeyModifiers.ControlShift => "Ctrl + Shift",
        ZoneHotkeyModifiers.AltShift => "Alt + Shift",
        ZoneHotkeyModifiers.ControlWin => "Ctrl + Win",
        _ => "Ctrl + Alt"
    };

    public OverlayLabelStyle OverlayLabelStyle
    {
        get => overlayLabelStyle;
        set => SetProperty(ref overlayLabelStyle, value);
    }

    public int OverlayBorderThickness
    {
        get => overlayBorderThickness;
        set => SetProperty(ref overlayBorderThickness, Math.Clamp(value, 1, 6));
    }

    public int OverlayCornerRadius
    {
        get => overlayCornerRadius;
        set => SetProperty(ref overlayCornerRadius, Math.Clamp(value, 0, 24));
    }

    public int OverlayLabelFontSize
    {
        get => overlayLabelFontSize;
        set => SetProperty(ref overlayLabelFontSize, Math.Clamp(value, 10, 24));
    }

    /// <summary>Farbe der hervorgehobenen Zone als #RRGGBB; leer bedeutet: dieselbe wie die Zonenfarbe.</summary>
    public string HighlightColor
    {
        get => highlightColor;
        set => SetProperty(ref highlightColor, value?.Trim() ?? string.Empty);
    }

    public double HighlightOpacityPercent
    {
        get => highlightOpacityPercent;
        set => SetProperty(ref highlightOpacityPercent, NormalizePercent(value, 10, 90));
    }

    /// <summary>Wie viele Verschiebe-Ereignisse in zehn Sekunden der Schutzschalter zulaesst; 100 bis 5000.</summary>
    public int MoveHookEventLimit
    {
        get => moveHookEventLimit;
        set => SetProperty(ref moveHookEventLimit, Math.Clamp(value, 100, 5000));
    }

    /// <summary>Nach wie vielen Sekunden ein Ziehen ohne Endereignis abgebrochen wird; 5 bis 600.</summary>
    public int DragWatchdogSeconds
    {
        get => dragWatchdogSeconds;
        set => SetProperty(ref dragWatchdogSeconds, Math.Clamp(value, 5, 600));
    }

    public AppSettings CreateSettings() => new(
        ActiveProfileId: Guid.Empty,
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
        CheckForUpdatesOnStart: CheckForUpdatesOnStart,
        ElevationMode: ElevationMode,
        ShowAdvancedSettings: ShowAdvancedSettings,
        OverlayShowDelayMilliseconds: OverlayShowDelayMilliseconds,
        ActivateWindowAfterSnap: ActivateWindowAfterSnap,
        RestoreSizeWhenLeavingZone: RestoreSizeWhenLeavingZone,
        FixedSizeWindowPlacement: FixedSizeWindowPlacement,
        PlacementTolerancePixels: PlacementTolerancePixels,
        SnappedTolerancePixels: SnappedTolerancePixels,
        CatchNewWindowsInMainZone: CatchNewWindowsInMainZone,
        PreferRememberedZone: PreferRememberedZone,
        RestoreMaximizedWindows: RestoreMaximizedWindows,
        RememberedWindowLimit: RememberedWindowLimit,
        NewWindowSettleDelayMilliseconds: NewWindowSettleDelayMilliseconds,
        RuleRetryDelayMilliseconds: RuleRetryDelayMilliseconds,
        ZoneHotkeysEnabled: ZoneHotkeysEnabled,
        ZoneHotkeyModifiers: ZoneHotkeyModifiers,
        OverlayLabelStyle: OverlayLabelStyle,
        OverlayBorderThickness: OverlayBorderThickness,
        OverlayCornerRadius: OverlayCornerRadius,
        OverlayLabelFontSize: OverlayLabelFontSize,
        HighlightColor: HighlightColor,
        HighlightOpacity: HighlightOpacityPercent / 100d,
        MoveHookEventLimit: MoveHookEventLimit,
        DragWatchdogSeconds: DragWatchdogSeconds);

    public void Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        StartWithWindows = settings.StartWithWindows;
        RememberWindowPositions = settings.RememberWindowPositions;
        CheckForUpdatesOnStart = settings.CheckForUpdatesOnStart;
        ElevationMode = settings.ElevationMode;
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
        ShowAdvancedSettings = settings.ShowAdvancedSettings;
        OverlayShowDelayMilliseconds = settings.OverlayShowDelayMilliseconds;
        ActivateWindowAfterSnap = settings.ActivateWindowAfterSnap;
        RestoreSizeWhenLeavingZone = settings.RestoreSizeWhenLeavingZone;
        FixedSizeWindowPlacement = settings.FixedSizeWindowPlacement;
        PlacementTolerancePixels = settings.PlacementTolerancePixels;
        SnappedTolerancePixels = settings.SnappedTolerancePixels;
        CatchNewWindowsInMainZone = settings.CatchNewWindowsInMainZone;
        PreferRememberedZone = settings.PreferRememberedZone;
        RestoreMaximizedWindows = settings.RestoreMaximizedWindows;
        RememberedWindowLimit = settings.RememberedWindowLimit;
        NewWindowSettleDelayMilliseconds = settings.NewWindowSettleDelayMilliseconds;
        RuleRetryDelayMilliseconds = settings.RuleRetryDelayMilliseconds;
        ZoneHotkeysEnabled = settings.ZoneHotkeysEnabled;
        ZoneHotkeyModifiers = settings.ZoneHotkeyModifiers;
        OverlayLabelStyle = settings.OverlayLabelStyle;
        OverlayBorderThickness = settings.OverlayBorderThickness;
        OverlayCornerRadius = settings.OverlayCornerRadius;
        OverlayLabelFontSize = settings.OverlayLabelFontSize;
        HighlightColor = settings.HighlightColor ?? string.Empty;
        HighlightOpacityPercent = settings.HighlightOpacity * 100d;
        MoveHookEventLimit = settings.MoveHookEventLimit;
        DragWatchdogSeconds = settings.DragWatchdogSeconds;
    }

    /// <summary>
    /// Setzt alles auf die Voreinstellung zurueck. Erscheinungsbild, Autostart und Rechte bleiben, weil sie
    /// nicht zur Feinabstimmung gehoeren und ein Neustart daran haengt.
    /// </summary>
    public void ResetToDefaults()
    {
        var defaults = AppSettings.Default(Guid.Empty) with
        {
            ThemeMode = ThemeMode,
            StartWithWindows = StartWithWindows,
            ElevationMode = ElevationMode,
            CheckForUpdatesOnStart = CheckForUpdatesOnStart,
            ShowAdvancedSettings = ShowAdvancedSettings
        };
        Apply(defaults);
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
