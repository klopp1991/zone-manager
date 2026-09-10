using SnapZones.Core.Updates;
using Xunit;

namespace SnapZones.Tests.Updates;

/// <summary>
/// Der Geraetecode-Ablauf von GitHub. Ueber die Sitzung des Browsers kommt eine Anwendung nicht an ein
/// privates Repository; der Geraetecode ist der offizielle Weg und braucht nur einen Klick im Browser.
/// Geprueft wird hier die Auswertung der Antworten, ohne Netzwerk.
/// </summary>
public sealed class GitHubDeviceFlowTests
{
    [Fact]
    public void The_first_answer_carries_code_address_and_timings()
    {
        var code = GitHubDeviceFlow.ParseDeviceCode(
            """
            {
              "device_code": "3584d83530557fdd1f46af8289938c8ef79f9dc5",
              "user_code": "WDJB-MJHT",
              "verification_uri": "https://github.com/login/device",
              "expires_in": 900,
              "interval": 5
            }
            """);

        Assert.NotNull(code);
        Assert.Equal("WDJB-MJHT", code.UserCode);
        Assert.Equal("3584d83530557fdd1f46af8289938c8ef79f9dc5", code.DeviceCode);
        Assert.Equal("https://github.com/login/device", code.VerificationUri);
        Assert.Equal(TimeSpan.FromSeconds(5), code.Interval);
        Assert.Equal(TimeSpan.FromMinutes(15), code.ExpiresIn);
    }

    [Fact]
    public void An_unusable_first_answer_yields_nothing()
    {
        Assert.Null(GitHubDeviceFlow.ParseDeviceCode(""));
        Assert.Null(GitHubDeviceFlow.ParseDeviceCode("kein JSON"));
        Assert.Null(GitHubDeviceFlow.ParseDeviceCode("""{"user_code":"WDJB-MJHT"}"""));
    }

    [Fact]
    public void Waiting_keeps_the_interval_and_slowing_down_raises_it()
    {
        var pending = GitHubDeviceFlow.ParsePollResponse(
            """{"error":"authorization_pending"}""",
            TimeSpan.FromSeconds(5));
        Assert.Equal(GitHubPollOutcome.Pending, pending.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(5), pending.Interval);

        var slower = GitHubDeviceFlow.ParsePollResponse(
            """{"error":"slow_down","interval":10}""",
            TimeSpan.FromSeconds(5));
        Assert.Equal(GitHubPollOutcome.SlowDown, slower.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(10), slower.Interval);
    }

    [Fact]
    public void A_confirmed_code_hands_over_the_access_token()
    {
        var granted = GitHubDeviceFlow.ParsePollResponse(
            """{"access_token":"gho_beispiel","token_type":"bearer","scope":"repo"}""",
            TimeSpan.FromSeconds(5));

        Assert.Equal(GitHubPollOutcome.Granted, granted.Outcome);
        Assert.Equal("gho_beispiel", granted.AccessToken);
    }

    [Theory]
    [InlineData("expired_token", GitHubPollOutcome.Expired)]
    [InlineData("access_denied", GitHubPollOutcome.Denied)]
    [InlineData("unsupported_grant_type", GitHubPollOutcome.Failed)]
    public void Every_ending_carries_a_sentence_in_plain_language(string error, GitHubPollOutcome expected)
    {
        var result = GitHubDeviceFlow.ParsePollResponse($$"""{"error":"{{error}}"}""", TimeSpan.FromSeconds(5));

        Assert.Equal(expected, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.Null(result.AccessToken);
    }

    [Fact]
    public void The_remaining_time_reads_as_minutes_and_seconds()
    {
        Assert.Equal("14:59", GitHubDeviceFlow.DescribeRemaining(TimeSpan.FromSeconds(899)));
        Assert.Equal("00:07", GitHubDeviceFlow.DescribeRemaining(TimeSpan.FromSeconds(7)));
        Assert.Equal("00:00", GitHubDeviceFlow.DescribeRemaining(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Both_requests_carry_exactly_the_fields_github_expects()
    {
        var first = GitHubDeviceFlow.DeviceCodeRequest("client");
        Assert.Contains(first, field => field.Key == "client_id" && field.Value == "client");
        Assert.Contains(first, field => field.Key == "scope" && field.Value == "repo");

        var poll = GitHubDeviceFlow.AccessTokenRequest("client", "device");
        Assert.Contains(poll, field => field.Key == "device_code" && field.Value == "device");
        Assert.Contains(
            poll,
            field => field.Key == "grant_type" && field.Value == "urn:ietf:params:oauth:grant-type:device_code");
    }
}
