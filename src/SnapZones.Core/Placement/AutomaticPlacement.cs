namespace SnapZones.Core.Placement;

/// <summary>Warum ein Fenster nicht von selbst platziert wird.</summary>
public enum AutomaticPlacementRejection
{
    None,

    /// <summary>Menue, Tooltip, Aufklappliste oder ein anderes kurzlebiges Fenster.</summary>
    TransientClass,

    /// <summary>Keine Titelleiste. Kein Fenster, das ein Benutzer selbst herumschieben wuerde.</summary>
    NoCaption,

    /// <summary>Die Groesse laesst sich nicht aendern; das Fenster kann eine Zone gar nicht fuellen.</summary>
    NotResizable,

    /// <summary>Kein Maximieren moeglich: das Merkmal, an dem sich ein Dialog von einem Programmfenster trennt.</summary>
    NoMaximizeBox,

    /// <summary>Das Fenster gehoert einem anderen: ein Dialog, eine Palette, ein Hinweisfenster.</summary>
    Owned,

    /// <summary>Zu klein, um sinnvoll in einer Zone zu liegen.</summary>
    TooSmall
}

/// <summary>Die Merkmale eines Fensters, soweit sie fuer das automatische Platzieren zaehlen.</summary>
public sealed record AutomaticPlacementCandidate(
    string WindowClass,
    bool HasCaption,
    bool IsResizable,
    bool HasMaximizeBox,
    bool HasOwner,
    int Width,
    int Height);

/// <summary>
/// Der Filter fuer alles, was das Programm <em>von selbst</em> anfasst: den Auffang in der Hauptzone,
/// das Wiederherstellen gemerkter Positionen, den Auffang nach einem Layoutwechsel und das Nachziehen
/// bei geaenderten Zonen.
///
/// <para>
/// Bis zum 03.09.2026 galt hier derselbe grosszuegige Filter wie fuer ein vom Benutzer gezogenes
/// Fenster: es genuegte, dass ein Popup irgendeinen Rahmenstil trug. Damit wurden Kontextmenue- und
/// Aufklappfenster moderner Oberflaechen und jeder Dialog mit Titelleiste — bis hin zum Kopierdialog
/// des Explorers — in die Hauptzone gezogen.
/// </para>
///
/// <para>
/// Bewusst nur fuer den automatischen Weg. Zieht der Benutzer ein Fenster selbst auf eine Zone oder
/// drueckt er ein Zonenkuerzel, bleibt jedes Fenster erlaubt: dort ist die Absicht eindeutig.
/// </para>
/// </summary>
public static class AutomaticPlacement
{
    /// <summary>Kleiner darf ein Fenster nicht sein, damit es von selbst in eine Zone gelegt wird.</summary>
    public const int MinimumWidth = 200;

    public const int MinimumHeight = 120;

    /// <summary>
    /// Fensterklassen, hinter denen nie ein Programmfenster steckt: das Win32-Menue, Tooltips,
    /// Aufklapplisten, die Vorschlagsliste der Adressleiste, die Popup-Wirte von XAML und WinUI und der
    /// Schattenwurf. Der Vergleich ist unabhaengig von Gross- und Kleinschreibung.
    /// </summary>
    public static readonly IReadOnlySet<string> TransientWindowClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "#32768",
        "#32770",
        "tooltips_class32",
        "ComboLBox",
        "Auto-Suggest Dropdown",
        "DV2ControlHost",
        "Xaml_WindowedPopupClass",
        "Microsoft.UI.Content.PopupWindowSiteBridge",
        "SysShadow",
        "MsoCommandBar"
    };

    /// <summary>Ob das Programm dieses Fenster von sich aus verschieben darf.</summary>
    public static bool IsEligible(AutomaticPlacementCandidate candidate) =>
        Evaluate(candidate) == AutomaticPlacementRejection.None;

    /// <summary>
    /// Die Pruefung mit Begruendung. Die Reihenfolge ist nach Aussagekraft gewaehlt: die Fensterklasse
    /// zuerst, weil sie am eindeutigsten ist, die Groesse zuletzt.
    /// </summary>
    public static AutomaticPlacementRejection Evaluate(AutomaticPlacementCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (TransientWindowClasses.Contains(candidate.WindowClass))
        {
            return AutomaticPlacementRejection.TransientClass;
        }

        if (!candidate.HasCaption)
        {
            return AutomaticPlacementRejection.NoCaption;
        }

        if (candidate.HasOwner)
        {
            return AutomaticPlacementRejection.Owned;
        }

        if (!candidate.IsResizable)
        {
            return AutomaticPlacementRejection.NotResizable;
        }

        // Ein Fenster ohne Maximieren-Schaltflaeche ist im Sprachgebrauch von Windows ein Dialog: der
        // Kopierdialog des Explorers, ein Einstellungsfenster, eine Rueckfrage. Es hat zwar Titelleiste
        // und Rahmen, aber es gehoert nicht in eine Zone.
        if (!candidate.HasMaximizeBox)
        {
            return AutomaticPlacementRejection.NoMaximizeBox;
        }

        return candidate.Width < MinimumWidth || candidate.Height < MinimumHeight
            ? AutomaticPlacementRejection.TooSmall
            : AutomaticPlacementRejection.None;
    }

    /// <summary>Die Begruendung in Worten, fuer das Protokoll.</summary>
    public static string Describe(AutomaticPlacementRejection rejection) => rejection switch
    {
        AutomaticPlacementRejection.TransientClass => "kurzlebiges Fenster (Menü, Tooltip, Aufklappliste)",
        AutomaticPlacementRejection.NoCaption => "keine Titelleiste",
        AutomaticPlacementRejection.NotResizable => "Größe nicht veränderbar",
        AutomaticPlacementRejection.NoMaximizeBox => "Dialog ohne Maximieren-Schaltfläche",
        AutomaticPlacementRejection.Owned => "gehört einem anderen Fenster",
        AutomaticPlacementRejection.TooSmall => "kleiner als das Mindestmass",
        _ => "keine"
    };
}
