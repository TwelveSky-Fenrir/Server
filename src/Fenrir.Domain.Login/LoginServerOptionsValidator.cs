using Microsoft.Extensions.Options;

namespace Fenrir.Domain.Login;

public sealed class LoginServerOptionsValidator : IValidateOptions<LoginServerOptions>
{
    public ValidateOptionsResult Validate(string? name, LoginServerOptions options)
    {
        var errors = new List<string>();

        if (options.Port is <= 0 or > 65535)
            errors.Add($"Login:Port must be between 1 and 65535 (was {options.Port}).");
        if (options.ExpectedClientVersion <= 0)
            errors.Add($"Login:ExpectedClientVersion must be positive (was {options.ExpectedClientVersion}).");
        if (options.TicketTtlSeconds <= 0)
            errors.Add($"Login:TicketTtlSeconds must be positive (was {options.TicketTtlSeconds}).");
        if (options.ShardReachabilityProbeTimeoutMilliseconds <= 0)
            errors.Add(
                $"Login:ShardReachabilityProbeTimeoutMilliseconds must be positive (was {options.ShardReachabilityProbeTimeoutMilliseconds}).");
        if (options.IdleSweepIntervalSeconds <= 0)
            errors.Add($"Login:IdleSweepIntervalSeconds must be positive (was {options.IdleSweepIntervalSeconds}).");
        if (options.PreAuthenticationHandshakeTimeoutSeconds is < 1 or > 30)
            errors.Add(
                $"Login:PreAuthenticationHandshakeTimeoutSeconds must be between 1 and 30 (was {options.PreAuthenticationHandshakeTimeoutSeconds}).");
        if (options.IdleSweepIntervalSeconds > options.PreAuthenticationHandshakeTimeoutSeconds)
            errors.Add(
                $"Login:IdleSweepIntervalSeconds must not exceed Login:PreAuthenticationHandshakeTimeoutSeconds (was {options.IdleSweepIntervalSeconds} and {options.PreAuthenticationHandshakeTimeoutSeconds}).");
        if (options.MaxConnectionsPerIp <= 0)
            errors.Add($"Login:MaxConnectionsPerIp must be positive (was {options.MaxConnectionsPerIp}).");
        if (options.MaxProtocolViolationsPerIpPerHour <= 0)
            errors.Add(
                $"Login:MaxProtocolViolationsPerIpPerHour must be positive (was {options.MaxProtocolViolationsPerIpPerHour}).");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
