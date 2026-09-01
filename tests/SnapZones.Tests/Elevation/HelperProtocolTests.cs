using SnapZones.Core.Elevation;
using Xunit;

namespace SnapZones.Tests.Elevation;

/// <summary>
/// Das Hilfsprogramm läuft mit dem Recht, auch höher berechtigte Fenster anzufassen. Was es entgegennimmt,
/// muss deshalb streng geprüft sein: es gibt keine Nachsicht, keine Vorgabewerte und nichts zu erraten.
/// </summary>
public sealed class HelperProtocolTests
{
    [Fact]
    public void A_placement_survives_the_round_trip_unchanged()
    {
        var line = HelperProtocol.BuildPlace((nint)0x1234, 100, -50, 1920, 1040);

        Assert.True(HelperProtocol.TryParseRequest(line, out var request));
        Assert.Equal(HelperVerb.Place, request.Verb);
        Assert.Equal((nint)0x1234, request.WindowHandle);
        Assert.Equal(100, request.X);
        Assert.Equal(-50, request.Y);
        Assert.Equal(1920, request.Width);
        Assert.Equal(1040, request.Height);
    }

    [Fact]
    public void A_ping_is_answered_with_the_protocol_version()
    {
        Assert.True(HelperProtocol.TryParseRequest(HelperProtocol.BuildPing(), out var request));
        Assert.Equal(HelperVerb.Ping, request.Verb);

        Assert.True(HelperProtocol.TryParsePong(HelperProtocol.BuildPong(), out var version));
        Assert.Equal(HelperProtocol.Version, version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("PLACE")]
    [InlineData("PLACE 1 2 3 4")]
    [InlineData("PLACE 1 2 3 4 5 6")]
    [InlineData("place 1 0 0 100 100")]
    [InlineData("VERSCHIEBE 1 0 0 100 100")]
    [InlineData("PLACE null 0 0 100 100")]
    [InlineData("PLACE 0 0 0 100 100")]
    [InlineData("PING PONG")]
    public void Anything_that_is_not_exactly_a_known_command_is_refused(string? line) =>
        Assert.False(HelperProtocol.TryParseRequest(line, out _));

    [Theory]
    [InlineData("PLACE 1 0 0 0 100")]
    [InlineData("PLACE 1 0 0 100 0")]
    [InlineData("PLACE 1 0 0 -10 100")]
    public void A_window_without_area_is_refused(string line) =>
        Assert.False(HelperProtocol.TryParseRequest(line, out _));

    [Theory]
    [InlineData("PLACE 1 1000001 0 100 100")]
    [InlineData("PLACE 1 0 -1000001 100 100")]
    [InlineData("PLACE 1 0 0 2000000 100")]
    [InlineData("PLACE 1 0 0 100 9999999999")]
    public void Coordinates_beyond_any_conceivable_screen_are_refused(string line)
    {
        // Die Grenze haelt einen Zahlenueberlauf im Empfaenger von vornherein aus dem Weg.
        Assert.False(HelperProtocol.TryParseRequest(line, out _));
    }

    [Fact]
    public void The_edge_of_the_allowed_range_is_still_accepted()
    {
        var line = HelperProtocol.BuildPlace(
            (nint)1,
            HelperProtocol.CoordinateLimit,
            -HelperProtocol.CoordinateLimit,
            1,
            1);

        Assert.True(HelperProtocol.TryParseRequest(line, out _));
    }

    [Fact]
    public void A_failure_reason_never_breaks_the_line_structure()
    {
        // Eine Begruendung mit Zeilenumbruch wuerde als zusaetzliche Antwort gelesen.
        var failure = HelperProtocol.BuildFailure("Erste Zeile\nZweite Zeile\rDritte");

        Assert.DoesNotContain('\n', failure);
        Assert.DoesNotContain('\r', failure);
        Assert.Equal("Erste Zeile Zweite Zeile Dritte", HelperProtocol.ReadFailureReason(failure));
    }

    [Fact]
    public void An_overlong_reason_is_shortened_and_an_empty_one_is_named()
    {
        Assert.True(HelperProtocol.BuildFailure(new string('x', 500)).Length < 260);
        Assert.Equal("Unbekannter Grund", HelperProtocol.ReadFailureReason(HelperProtocol.BuildFailure("   ")));
    }

    [Fact]
    public void Success_is_recognised_and_nothing_else_is()
    {
        Assert.True(HelperProtocol.IsSuccess("OK"));
        Assert.True(HelperProtocol.IsSuccess(" OK "));
        Assert.False(HelperProtocol.IsSuccess("ok"));
        Assert.False(HelperProtocol.IsSuccess("OKAY"));
        Assert.False(HelperProtocol.IsSuccess(null));
        Assert.False(HelperProtocol.IsSuccess(HelperProtocol.BuildFailure("Grund")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("PONG")]
    [InlineData("PONG x")]
    [InlineData("PONG 1 2")]
    [InlineData("OK 1")]
    public void An_unexpected_greeting_is_not_taken_for_a_version(string? reply) =>
        Assert.False(HelperProtocol.TryParsePong(reply, out _));

    [Fact]
    public void Reading_a_reason_from_something_that_is_not_a_failure_yields_nothing()
    {
        Assert.Equal(string.Empty, HelperProtocol.ReadFailureReason("OK"));
        Assert.Equal(string.Empty, HelperProtocol.ReadFailureReason(null));
        Assert.Equal(string.Empty, HelperProtocol.ReadFailureReason("FAIL"));
    }
}
