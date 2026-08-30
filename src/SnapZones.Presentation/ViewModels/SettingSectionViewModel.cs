using SnapZones.Core.Settings;

namespace SnapZones.Presentation.ViewModels;

/// <summary>
/// One titled section of the settings page. Hides itself when the current
/// settings search filters out all of its settings.
/// </summary>
public sealed class SettingSectionViewModel : ViewModelBase
{
    private bool isVisible = true;

    public SettingSectionViewModel(SettingCategory category, IReadOnlyList<SettingFieldViewModel> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        Category = category;
        Fields = fields;
        Title = SettingsCatalog.CategoryLabel(category);
        Description = SettingsCatalog.CategoryDescription(category);
    }

    public SettingCategory Category { get; }
    public IReadOnlyList<SettingFieldViewModel> Fields { get; }
    public string Title { get; }
    public string Description { get; }

    /// <summary>False while every setting in this section is filtered out.</summary>
    public bool IsVisible
    {
        get => isVisible;
        private set => SetProperty(ref isVisible, value);
    }

    internal void RefreshVisibility() => IsVisible = Fields.Any(field => field.IsVisible);
}
