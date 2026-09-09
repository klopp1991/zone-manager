using System.Windows;
using System.Windows.Controls;

namespace SnapZones.App.Controls;

/// <summary>Der Zustand, den ein <see cref="StatusChip"/> zeigt.</summary>
public enum StatusChipState
{
    /// <summary>Alles eingerichtet: gruener Punkt, etwa «Aktiv», «Installiert», «Verbunden».</summary>
    Good,

    /// <summary>Etwas fehlt oder ist eingeschraenkt: oranger Punkt.</summary>
    Warning,

    /// <summary>Nicht eingerichtet oder ausgeschaltet: leerer Punkt in Grau.</summary>
    Neutral
}

/// <summary>
/// Die Pille hinter einer Beschriftung, die den Zustand einer Systemsache in einem Wort zeigt.
/// Die Farbe traegt den Zustand nie allein: jedem Zustand gehoert ein eigener Punkt (● oder ○)
/// und ein eigenes Wort. Das Chip bricht nie um, die Beschriftungszeile darum herum darf es.
/// </summary>
public sealed class StatusChip : System.Windows.Controls.Control
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(StatusChipState),
        typeof(StatusChip),
        new FrameworkPropertyMetadata(StatusChipState.Neutral, FrameworkPropertyMetadataOptions.AffectsRender, StateChanged));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(StatusChip),
        new FrameworkPropertyMetadata(string.Empty, TextChanged));

    private static readonly DependencyPropertyKey GlyphPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Glyph),
        typeof(string),
        typeof(StatusChip),
        new FrameworkPropertyMetadata("○"));

    public static readonly DependencyProperty GlyphProperty = GlyphPropertyKey.DependencyProperty;

    static StatusChip()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(StatusChip), new FrameworkPropertyMetadata(typeof(StatusChip)));
    }

    public StatusChipState State
    {
        get => (StatusChipState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Der Punkt vor dem Wort: gefuellt bei Gut und Achtung, leer bei Neutral.</summary>
    public string Glyph => (string)GetValue(GlyphProperty);

    private static void StateChanged(DependencyObject element, DependencyPropertyChangedEventArgs eventArgs)
    {
        var chip = (StatusChip)element;
        chip.SetValue(GlyphPropertyKey, (StatusChipState)eventArgs.NewValue == StatusChipState.Neutral ? "○" : "●");
        chip.UpdateAutomationName();
    }

    private static void TextChanged(DependencyObject element, DependencyPropertyChangedEventArgs eventArgs)
    {
        _ = eventArgs;
        ((StatusChip)element).UpdateAutomationName();
    }

    private void UpdateAutomationName() =>
        System.Windows.Automation.AutomationProperties.SetName(this, Text ?? string.Empty);
}
