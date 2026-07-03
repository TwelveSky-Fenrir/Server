using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login;

/// <summary>Validates <see cref="LoginServerOptions" /> at startup (ValidateOnStart) before any connection is accepted.</summary>
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
        if (options.MaxPlayerNum <= 0) errors.Add($"Login:MaxPlayerNum must be positive (was {options.MaxPlayerNum}).");

        // RequireSecondPassword needs no range rule (a bool has no invalid value); listed here so the validator
        // stays the single inventory of every Login:* knob. Prod EU33 runs with it true (P2ndPassword=1).

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
