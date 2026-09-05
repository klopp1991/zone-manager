using System.Windows;
using System.Windows.Media;

namespace SnapZones.Tests.Support;

/// <summary>
/// Sucht Elemente im visuellen Baum eines gezeigten Fensters. Noetig fuer alles, was in einer
/// Datenvorlage entsteht – etwa das aufgeklappte Detail einer Listenzeile –, weil FindName am Fenster
/// solche Elemente nicht kennt.
/// </summary>
internal static class UiTree
{
    public static IEnumerable<T> VisualDescendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in VisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    public static IEnumerable<T> LogicalDescendants<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in LogicalDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>Das erste sichtbare Element mit diesem Namen, oder eine aussagekraeftige Ausnahme.</summary>
    public static T Find<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        var match = VisualDescendants<T>(root).FirstOrDefault(element => element.Name == name);
        return match ?? throw new Xunit.Sdk.XunitException($"Das Element «{name}» ({typeof(T).Name}) steht nicht im visuellen Baum.");
    }

    public static T? TryFind<T>(DependencyObject root, string name) where T : FrameworkElement =>
        VisualDescendants<T>(root).FirstOrDefault(element => element.Name == name);
}
