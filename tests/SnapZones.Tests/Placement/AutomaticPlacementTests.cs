using SnapZones.Core.Placement;
using Xunit;

namespace SnapZones.Tests.Placement;

/// <summary>
/// Der Filter fuer alles, was das Programm von selbst anfasst (03.09.2026). Zuvor genuegte irgendein
/// Rahmenstil, sodass Kontextmenues moderner Oberflaechen und jeder Dialog mit Titelleiste — bis hin zum
/// Kopierdialog des Explorers — in die Hauptzone gezogen wurden.
/// </summary>
public sealed class AutomaticPlacementTests
{
    private static AutomaticPlacementCandidate ApplicationWindow(
        string windowClass = "ApplicationFrameWindow",
        bool hasCaption = true,
        bool isResizable = true,
        bool hasMaximizeBox = true,
        bool hasOwner = false,
        int width = 1200,
        int height = 800) =>
        new(windowClass, hasCaption, isResizable, hasMaximizeBox, hasOwner, width, height);

    [Fact]
    public void A_normal_application_window_is_placed_automatically()
    {
        Assert.True(AutomaticPlacement.IsEligible(ApplicationWindow()));
        Assert.Equal(AutomaticPlacementRejection.None, AutomaticPlacement.Evaluate(ApplicationWindow()));
    }

    [Theory]
    [InlineData("#32768")]
    [InlineData("#32770")]
    [InlineData("tooltips_class32")]
    [InlineData("ComboLBox")]
    [InlineData("Xaml_WindowedPopupClass")]
    [InlineData("AUTO-SUGGEST DROPDOWN")]
    public void Menus_tooltips_and_dialog_classes_are_never_placed(string windowClass)
    {
        var candidate = ApplicationWindow(windowClass: windowClass);

        Assert.Equal(AutomaticPlacementRejection.TransientClass, AutomaticPlacement.Evaluate(candidate));
    }

    [Fact]
    public void A_context_menu_without_a_title_bar_is_rejected_whatever_its_class()
    {
        // Ein Menuefenster einer fremden Oberflaeche traegt eine eigene Klasse; entscheidend ist, dass
        // ihm die Titelleiste fehlt.
        var candidate = ApplicationWindow(windowClass: "Chrome_WidgetWin_1", hasCaption: false);

        Assert.Equal(AutomaticPlacementRejection.NoCaption, AutomaticPlacement.Evaluate(candidate));
    }

    [Fact]
    public void An_owned_window_stays_where_it_appears()
    {
        var candidate = ApplicationWindow(hasOwner: true);

        Assert.Equal(AutomaticPlacementRejection.Owned, AutomaticPlacement.Evaluate(candidate));
    }

    [Fact]
    public void A_dialog_with_a_title_bar_but_no_maximize_button_is_left_alone()
    {
        // Der Kopierdialog des Explorers: Titelleiste, Rahmen, in der Groesse veraenderbar — aber ohne
        // Maximieren. Genau daran haengt der Unterschied zum Programmfenster.
        var candidate = ApplicationWindow(windowClass: "OperationStatusWindow", hasMaximizeBox: false);

        Assert.Equal(AutomaticPlacementRejection.NoMaximizeBox, AutomaticPlacement.Evaluate(candidate));
    }

    [Fact]
    public void A_window_with_a_fixed_size_cannot_fill_a_zone()
    {
        var candidate = ApplicationWindow(isResizable: false);

        Assert.Equal(AutomaticPlacementRejection.NotResizable, AutomaticPlacement.Evaluate(candidate));
    }

    [Theory]
    [InlineData(199, 800)]
    [InlineData(1200, 119)]
    public void A_window_below_the_minimum_size_is_left_alone(int width, int height)
    {
        var candidate = ApplicationWindow(width: width, height: height);

        Assert.Equal(AutomaticPlacementRejection.TooSmall, AutomaticPlacement.Evaluate(candidate));
    }

    [Fact]
    public void Every_rejection_has_a_reason_in_words()
    {
        foreach (var rejection in Enum.GetValues<AutomaticPlacementRejection>())
        {
            Assert.False(string.IsNullOrWhiteSpace(AutomaticPlacement.Describe(rejection)));
        }
    }
}
