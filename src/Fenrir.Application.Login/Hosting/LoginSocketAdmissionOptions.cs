using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Hosting;

// Server/Header/ini.h:178 et :245 : Login.Server/MaxUser -> mServerMaxUserNum, distinct du plafond de
// COMPTES mGAME.mMaxPlayerNum. Valeur livree : Server/BuildEU33/ServerInfo.ini:122.
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
