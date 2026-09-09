using System.Windows;
using System.Windows.Controls;

namespace SnapZones.App.Controls;

/// <summary>
/// Der Abschnitt «Feinabstimmung» am Fuss einer Seite. Zugeklappt ist er eine Zeile mit Pfeil,
/// Titel und einem Stichwort rechts; ein Klick auf die ganze Zeile schaltet um. Der Zustand gilt
/// nur fuer die laufende Sitzung und wird nicht gespeichert – Ausnahme ist ein Suchtreffer, der
/// in die Feinabstimmung fuehrt und sie beim Springen aufklappt.
/// </summary>
public sealed class TuningExpander : System.Windows.Controls.ContentControl
{
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded),
        typeof(bool),
        typeof(TuningExpander),
        new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(TuningExpander),
        new FrameworkPropertyMetadata("Feinabstimmung"));

    public static readonly DependencyProperty HintProperty = DependencyProperty.Register(
        nameof(Hint),
        typeof(string),
        typeof(TuningExpander),
        new FrameworkPropertyMetadata("Selten nötig – die Voreinstellungen passen für fast alle"));

    /// <summary>Der Name des Abschnitts, ueber den ein Suchtreffer ihn aufklappen laesst.</summary>
    public static readonly DependencyProperty SectionProperty = DependencyProperty.Register(
        nameof(Section),
        typeof(string),
        typeof(TuningExpander),
        new FrameworkPropertyMetadata(string.Empty));

    static TuningExpander()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TuningExpander), new FrameworkPropertyMetadata(typeof(TuningExpander)));
    }

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public string Section
    {
        get => (string)GetValue(SectionProperty);
        set => SetValue(SectionProperty, value);
    }
}
