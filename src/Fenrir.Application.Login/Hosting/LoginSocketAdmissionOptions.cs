using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Hosting;

public sealed class LoginSocketAdmissionOptions
{
    public int MaxConcurrentConnections { get; set; } = 1000;
}

public sealed class LoginSocketAdmissionOptionsValidator : IValidateOptions<LoginSocketAdmissionOptions>
{
    public ValidateOptionsResult Validate(string? name, LoginSocketAdmissionOptions options)
    {
        return options.MaxConcurrentConnections > 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Login:MaxConcurrentConnections must be positive (was {options.MaxConcurrentConnections}).");
    }
}
