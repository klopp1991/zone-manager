using System.Windows.Input;
using SnapZones.Core.Settings;

namespace SnapZones.Presentation.ViewModels;

/// <summary>
/// Presentation wrapper around one <see cref="SettingDescriptor"/>. Carries the
/// caption and help texts for the control, whether the setting still has its
/// default value, and whether the current settings search matches it.
/// </summary>
public sealed class SettingFieldViewModel : ViewModelBase
{
    private readonly Action resetToDefault;
    private bool isHelpExpanded;
    private bool isVisible = true;
    private bool isDefault = true;

    public SettingFieldViewModel(SettingDescriptor descriptor, Action resetToDefault)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(resetToDefault);

        Descriptor = descriptor;
        this.resetToDefault = resetToDefault;
        ResetCommand = new RelayCommand(() => this.resetToDefault(), () => !IsDefault);
        ToggleHelpCommand = new RelayCommand(() => IsHelpExpanded = !IsHelpExpanded);
    }

    public SettingDescriptor Descriptor { get; }

    public SettingKey Key => Descriptor.Key;
    public SettingCategory Category => Descriptor.Category;
    public string Label => Descriptor.Label;
    public string ShortHelp => Descriptor.ShortHelp;
    public string LongHelp => Descriptor.LongHelp;

    public bool IsNumeric => Descriptor.IsNumeric;
    public double Minimum => Descriptor.Range?.Minimum ?? 0;
    public double Maximum => Descriptor.Range?.Maximum ?? 0;
    public double Step => Descriptor.Range?.Step ?? 1;
    public string Unit => Descriptor.Range?.Unit ?? string.Empty;

    /// <summary>Accessible name for the help toggle, for example "Hilfe zu Deckkraft".</summary>
    public string HelpButtonName => $"Hilfe zu {Label}";

    /// <summary>Accessible name for the reset button.</summary>
    public string ResetButtonName => $"{Label} auf Standard zurücksetzen";

    /// <summary>
    /// Allowed values and factory value as one line, shown with the expanded
    /// help so the user can see what the control accepts.
    /// </summary>
    public string RangeSummary => Descriptor.Range is null
        ? string.Empty
        : $"Zulässig: {Descriptor.Range.DisplayRange} · Standard: {Descriptor.Range.DisplayDefault}";

    /// <summary>Whether the detailed help below the control is unfolded.</summary>
    public bool IsHelpExpanded
    {
        get => isHelpExpanded;
        set => SetProperty(ref isHelpExpanded, value);
    }

    /// <summary>False when the current settings search filters this setting out.</summary>
    public bool IsVisible
    {
        get => isVisible;
        internal set => SetProperty(ref isVisible, value);
    }

    /// <summary>True while the setting still holds its factory value.</summary>
    public bool IsDefault
    {
        get => isDefault;
        internal set
        {
            if (SetProperty(ref isDefault, value))
            {
                OnPropertyChanged(nameof(IsModified));
                (ResetCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>True when the user has moved the setting away from its default.</summary>
    public bool IsModified => !IsDefault;

    /// <summary>Restores the factory value of this single setting.</summary>
    public ICommand ResetCommand { get; }

    /// <summary>Folds the detailed help text in or out.</summary>
    public ICommand ToggleHelpCommand { get; }
}
