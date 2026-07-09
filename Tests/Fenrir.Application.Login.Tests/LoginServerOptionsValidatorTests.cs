using Fenrir.Application.Login.Domain;

namespace Fenrir.Application.Login.Tests;

public sealed class LoginServerOptionsValidatorTests
{
    private static LoginServerOptions Valid(
        int? port = null,
        int? expectedClientVersion = null,
        int? ticketTtlSeconds = null,
        int? idleSweepIntervalSeconds = null)
    {
        return new LoginServerOptions
        {
            Port = port ?? 29998,
            ExpectedClientVersion = expectedClientVersion ?? 90354,
            TicketTtlSeconds = ticketTtlSeconds ?? 15,
            IdleSweepIntervalSeconds = idleSweepIntervalSeconds ?? 1
        };
    }

    [Fact]
    public void Validate_WithDefaultOptions_Succeeds()
    {
        var result = new LoginServerOptionsValidator().Validate(null, Valid());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70000)]
    public void Validate_WithInvalidPort_FailsAndMentionsField(int port)
    {
        var options = Valid(port);

        var result = new LoginServerOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Login:Port"));
    }

    [Fact]
    public void Validate_WithZeroExpectedClientVersion_FailsAndMentionsField()
    {
        var options = Valid(expectedClientVersion: 0);

        var result = new LoginServerOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Login:ExpectedClientVersion"));
    }

    [Fact]
    public void Validate_WithZeroTicketTtlSeconds_FailsAndMentionsField()
    {
        var options = Valid(ticketTtlSeconds: 0);

        var result = new LoginServerOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Login:TicketTtlSeconds"));
    }

    [Fact]
    public void Validate_WithZeroIdleSweepIntervalSeconds_FailsAndMentionsField()
    {
        var options = Valid(idleSweepIntervalSeconds: 0);

        var result = new LoginServerOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Login:IdleSweepIntervalSeconds"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithNonPositiveMaxConnectionsPerIp_FailsAndMentionsField(int maxConnectionsPerIp)
    {
        var options = Valid();
        options.MaxConnectionsPerIp = maxConnectionsPerIp;

        var result = new LoginServerOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Login:MaxConnectionsPerIp"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithNonPositiveMaxProtocolViolationsPerIpPerHour_FailsAndMentionsField(
        int maxProtocolViolationsPerIpPerHour)
    {
        var options = Valid();
        options.MaxProtocolViolationsPerIpPerHour = maxProtocolViolationsPerIpPerHour;

        var result = new LoginServerOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Login:MaxProtocolViolationsPerIpPerHour"));
    }
}
