using System.Collections.ObjectModel;
using System.Windows.Input;
using SnapZones.Core.Models;
using SnapZones.Core.Settings;

namespace SnapZones.Presentation.ViewModels;

/// <summary>
/// Editable view of <see cref="AppSettings"/>.
/// <para>
/// Every numeric setting is held in the unit the user actually sees (pixels or
/// percent) and is clamped through the matching <see cref="SettingsCatalog"/>
/// range, so the control, the stored value and the help text can never disagree
/// about what is allowed.
/// </para>
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly Dictionary<SettingKey, SettingFieldViewModel> fields = [];

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
    private string searchTerm = string.Empty;
    private bool suppressValueChanged;

    public SettingsViewModel(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        foreach (var descriptor in SettingsCatalog.All)
        {
            var key = descriptor.Key;
            fields[key] = new SettingFieldViewModel(descriptor, () => ResetToDefault(key));
        }

        Sections = new ObservableCollection<SettingSectionViewModel>(
            Enum.GetValues<SettingCategory>()
                .Select(category => new SettingSectionViewModel(
                    category,
                    SettingsCatalog.InCategory(category).Select(descriptor => fields[descriptor.Key]).ToArray())));

        ResetAllCommand = new RelayCommand(ResetAll, () => IsAnySettingModified);
        ClearSearchCommand = new RelayCommand(() => SearchTerm = string.Empty, () => SearchTerm.Length > 0);

        overlayColor = SettingsCatalog.DefaultOverlayColor;
        ApplyCore(settings);
    }

    /// <summary>
    /// Raised when a stored setting changes. Deliberately separate from
    /// <see cref="ViewModelBase.PropertyChanged"/> so that purely visual state
    /// — the search term, an unfolded help text — never triggers a save.
    /// </summary>
    public event EventHandler? ValueChanged;

    public IReadOnlyList<OverlayScope> OverlayScopes { get; } = Enum.GetValues<OverlayScope>();
    public IReadOnlyList<TriggerMode> TriggerModes { get; } = Enum.GetValues<TriggerMode>();
    public IReadOnlyList<ThemeMode> ThemeModes { get; } = Enum.GetValues<ThemeMode>();

    /// <summary>The settings grouped into the sections the page renders.</summary>
    public ObservableCollection<SettingSectionViewModel> Sections { get; }

    public SettingFieldViewModel Field(SettingKey key) => fields[key];

    public SettingFieldViewModel ThemeField => fields[SettingKey.ThemeMode];
    public SettingFieldViewModel StartWithWindowsField => fields[SettingKey.StartWithWindows];
    public SettingFieldViewModel OverlayScopeField => fields[SettingKey.OverlayScope];
    public SettingFieldViewModel TriggerModeField => fields[SettingKey.TriggerMode];
    public SettingFieldViewModel ShowZoneNamesField => fields[SettingKey.ShowZoneNames];
    public SettingFieldViewModel OverlayColorField => fields[SettingKey.OverlayColor];
    public SettingFieldViewModel OverlayOpacityField => fields[SettingKey.OverlayOpacity];
    public SettingFieldViewModel OuterMarginsField => fields[SettingKey.OuterMargins];
    public SettingFieldViewModel ZoneGapField => fields[SettingKey.ZoneGap];
    public SettingFieldViewModel MagnetThresholdField => fields[SettingKey.MagnetThreshold];

    /// <summary>Free-text filter over the settings page. Empty shows everything.</summary>
    public string SearchTerm
    {
        get => searchTerm;
        set
        {
            if (SetProperty(ref searchTerm, value ?? string.Empty))
            {
                ApplySearch();
                (ClearSearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(HasSearchResults));
                OnPropertyChanged(nameof(SearchResultSummary));
            }
        }
    }

    /// <summary>False when the current search term matches no setting at all.</summary>
    public bool HasSearchResults => Sections.Any(section => section.IsVisible);

    /// <summary>Status line shown while a search is active.</summary>
    public string SearchResultSummary
    {
        get
        {
            if (SearchTerm.Length == 0)
            {
                return string.Empty;
            }

            var matches = fields.Values.Count(field => field.IsVisible);
            return matches switch
            {
                0 => $"Keine Einstellung passt zu «{SearchTerm}».",
                1 => "1 Einstellung gefunden.",
                _ => $"{matches} Einstellungen gefunden."
            };
        }
    }

    /// <summary>True when at least one setting differs from its factory value.</summary>
    public bool IsAnySettingModified => fields.Values.Any(field => field.IsModified);

    /// <summary>Restores the factory value of every setting on this page.</summary>
    public ICommand ResetAllCommand { get; }

    /// <summary>Empties the settings search box.</summary>
    public ICommand ClearSearchCommand { get; }

    public bool StartWithWindows
    {
        get => startWithWindows;
        set => SetValue(ref startWithWindows, value);
    }

    public OverlayScope OverlayScope
    {
        get => overlayScope;
        set => SetValue(ref overlayScope, value);
    }

    public TriggerMode TriggerMode
    {
        get => triggerMode;
        set => SetValue(ref triggerMode, value);
    }

    public ThemeMode ThemeMode
    {
        get => themeMode;
        set => SetValue(ref themeMode, value);
    }

    /// <summary>Sets all four overlay margins to the same value.</summary>
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
        set => SetValue(ref outerMarginLeft, SettingsCatalog.OuterMarginRange.ClampToInt(value));
    }

    public int OuterMarginTop
    {
        get => outerMarginTop;
        set => SetValue(ref outerMarginTop, SettingsCatalog.OuterMarginRange.ClampToInt(value));
    }

    public int OuterMarginRight
    {
        get => outerMarginRight;
        set => SetValue(ref outerMarginRight, SettingsCatalog.OuterMarginRange.ClampToInt(value));
    }

    public int OuterMarginBottom
    {
        get => outerMarginBottom;
        set => SetValue(ref outerMarginBottom, SettingsCatalog.OuterMarginRange.ClampToInt(value));
    }

    /// <summary>Overlay gap between neighbouring zones, in pixels.</summary>
    public int ZoneGap
    {
        get => zoneGap;
        set => SetValue(ref zoneGap, SettingsCatalog.ZoneGapRange.ClampToInt(value));
    }

    /// <summary>Editor snapping distance, in pixels.</summary>
    public int MagnetThresholdPixels
    {
        get => magnetThresholdPixels;
        set => SetValue(ref magnetThresholdPixels, SettingsCatalog.MagnetThresholdRange.ClampToInt(value));
    }

    public bool ShowZoneNames
    {
        get => showZoneNames;
        set => SetValue(ref showZoneNames, value);
    }

    public string OverlayColor
    {
        get => overlayColor;
        set => SetValue(ref overlayColor, value ?? string.Empty);
    }

    /// <summary>Overlay opacity in percent, rounded to half a percent.</summary>
    public double OverlayOpacityPercent
    {
        get => overlayOpacityPercent;
        set => SetValue(ref overlayOpacityPercent, NormalizeOpacity(value));
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
            OuterMarginBottom));

    /// <summary>
    /// Loads a stored configuration into the page. Raises a single
    /// <see cref="ValueChanged"/> notification rather than one per property.
    /// </summary>
    public void Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        suppressValueChanged = true;
        try
        {
            ApplyCore(settings);
        }
        finally
        {
            suppressValueChanged = false;
        }

        RaiseValueChanged();
    }

    /// <summary>Restores the factory value of a single setting.</summary>
    public void ResetToDefault(SettingKey key)
    {
        switch (key)
        {
            case SettingKey.ThemeMode:
                ThemeMode = ThemeMode.System;
                break;
            case SettingKey.StartWithWindows:
                StartWithWindows = false;
                break;
            case SettingKey.OverlayScope:
                OverlayScope = OverlayScope.AllMonitors;
                break;
            case SettingKey.TriggerMode:
                TriggerMode = TriggerMode.Immediate;
                break;
            case SettingKey.ShowZoneNames:
                ShowZoneNames = true;
                break;
            case SettingKey.OverlayColor:
                OverlayColor = SettingsCatalog.DefaultOverlayColor;
                break;
            case SettingKey.OverlayOpacity:
                OverlayOpacityPercent = SettingsCatalog.OverlayOpacityRange.Default;
                break;
            case SettingKey.OuterMargins:
                OuterMargin = (int)SettingsCatalog.OuterMarginRange.Default;
                break;
            case SettingKey.ZoneGap:
                ZoneGap = (int)SettingsCatalog.ZoneGapRange.Default;
                break;
            case SettingKey.MagnetThreshold:
                MagnetThresholdPixels = (int)SettingsCatalog.MagnetThresholdRange.Default;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown setting.");
        }
    }

    /// <summary>Restores the factory value of every setting on this page.</summary>
    public void ResetAll()
    {
        suppressValueChanged = true;
        try
        {
            foreach (var key in fields.Keys.ToArray())
            {
                ResetToDefault(key);
            }
        }
        finally
        {
            suppressValueChanged = false;
        }

        RaiseValueChanged();
    }

    private void ApplyCore(AppSettings settings)
    {
        StartWithWindows = settings.StartWithWindows;
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
        RefreshModifiedState();
    }

    private bool SetValue<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref field, value, propertyName))
        {
            return false;
        }

        RefreshModifiedState();
        RaiseValueChanged();
        return true;
    }

    private void RaiseValueChanged()
    {
        if (!suppressValueChanged)
        {
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RefreshModifiedState()
    {
        foreach (var (key, field) in fields)
        {
            field.IsDefault = IsAtDefault(key);
        }

        OnPropertyChanged(nameof(IsAnySettingModified));
        (ResetAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private bool IsAtDefault(SettingKey key) => key switch
    {
        SettingKey.ThemeMode => ThemeMode == ThemeMode.System,
        SettingKey.StartWithWindows => !StartWithWindows,
        SettingKey.OverlayScope => OverlayScope == OverlayScope.AllMonitors,
        SettingKey.TriggerMode => TriggerMode == TriggerMode.Immediate,
        SettingKey.ShowZoneNames => ShowZoneNames,
        SettingKey.OverlayColor => string.Equals(
            OverlayColor,
            SettingsCatalog.DefaultOverlayColor,
            StringComparison.OrdinalIgnoreCase),
        SettingKey.OverlayOpacity => OverlayOpacityPercent == SettingsCatalog.OverlayOpacityRange.Default,
        SettingKey.OuterMargins =>
            outerMarginLeft == SettingsCatalog.OuterMarginRange.Default &&
            outerMarginTop == SettingsCatalog.OuterMarginRange.Default &&
            outerMarginRight == SettingsCatalog.OuterMarginRange.Default &&
            outerMarginBottom == SettingsCatalog.OuterMarginRange.Default,
        SettingKey.ZoneGap => ZoneGap == SettingsCatalog.ZoneGapRange.Default,
        SettingKey.MagnetThreshold => MagnetThresholdPixels == SettingsCatalog.MagnetThresholdRange.Default,
        _ => true
    };

    private void ApplySearch()
    {
        foreach (var field in fields.Values)
        {
            field.IsVisible = field.Descriptor.Matches(searchTerm);
        }

        foreach (var section in Sections)
        {
            section.RefreshVisibility();
        }
    }

    private static double NormalizeOpacity(double value)
    {
        var range = SettingsCatalog.OverlayOpacityRange;
        if (!double.IsFinite(value))
        {
            return range.Minimum;
        }

        var clamped = Math.Clamp(value, range.Minimum, range.Maximum);
        return Math.Round(clamped * 2, MidpointRounding.AwayFromZero) / 2;
    }
}
