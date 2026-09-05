using System.Windows;

namespace SnapZones.App.Controls;

/// <summary>
/// Angehaengte Eigenschaften, mit denen die Oberflaeche den Vorlagen in Theme.xaml Zusatzinformationen
/// mitgibt: Platzhaltertext eines Eingabefelds, Gruppenueberschrift und Zaehler eines Navigationseintrags,
/// der Kopfbereich der Seitenleiste und der «aktuelle» Zustand eines Layout-Tabs.
/// </summary>
public static class Chrome
{
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
        "Placeholder",
        typeof(string),
        typeof(Chrome),
        new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty SidebarHeaderProperty = DependencyProperty.RegisterAttached(
        "SidebarHeader",
        typeof(object),
        typeof(Chrome),
        new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty GroupProperty = DependencyProperty.RegisterAttached(
        "Group",
        typeof(string),
        typeof(Chrome),
        new FrameworkPropertyMetadata(string.Empty, GroupChanged));

    public static readonly DependencyProperty HasGroupProperty = DependencyProperty.RegisterAttached(
        "HasGroup",
        typeof(bool),
        typeof(Chrome),
        new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty BadgeProperty = DependencyProperty.RegisterAttached(
        "Badge",
        typeof(string),
        typeof(Chrome),
        new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsCurrentProperty = DependencyProperty.RegisterAttached(
        "IsCurrent",
        typeof(bool),
        typeof(Chrome),
        new FrameworkPropertyMetadata(false));

    public static string GetPlaceholder(DependencyObject element) => (string)element.GetValue(PlaceholderProperty);
    public static void SetPlaceholder(DependencyObject element, string value) => element.SetValue(PlaceholderProperty, value);

    public static object? GetSidebarHeader(DependencyObject element) => element.GetValue(SidebarHeaderProperty);
    public static void SetSidebarHeader(DependencyObject element, object? value) => element.SetValue(SidebarHeaderProperty, value);

    public static string GetGroup(DependencyObject element) => (string)element.GetValue(GroupProperty);
    public static void SetGroup(DependencyObject element, string value) => element.SetValue(GroupProperty, value);

    public static bool GetHasGroup(DependencyObject element) => (bool)element.GetValue(HasGroupProperty);
    public static void SetHasGroup(DependencyObject element, bool value) => element.SetValue(HasGroupProperty, value);

    public static string GetBadge(DependencyObject element) => (string)element.GetValue(BadgeProperty);
    public static void SetBadge(DependencyObject element, string value) => element.SetValue(BadgeProperty, value);

    public static bool GetIsCurrent(DependencyObject element) => (bool)element.GetValue(IsCurrentProperty);
    public static void SetIsCurrent(DependencyObject element, bool value) => element.SetValue(IsCurrentProperty, value);

    private static void GroupChanged(DependencyObject element, DependencyPropertyChangedEventArgs eventArgs) =>
        element.SetValue(HasGroupProperty, !string.IsNullOrWhiteSpace(eventArgs.NewValue as string));
}
