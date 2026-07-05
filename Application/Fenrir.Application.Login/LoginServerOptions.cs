namespace Fenrir.Application.Login;

/// <summary>
///     Bound from the <c>Login</c> config section; defaults match legacy <c>BuildEU33/ServerInfo.ini</c>, which the
///     real client requires.
/// </summary>
public sealed class LoginServerOptions
{
    public int Port { get; set; } = 29998;

    /// <summary>
    ///     Must match <c>LoginRequest.Version</c> or login fails with Result=4; legacy ini <c>[Server.Info].Version</c>
    ///     (not the "33" placeholder from wire contract §9.1).
    /// </summary>
    public int ExpectedClientVersion { get; set; } = 90354;

    public int TicketTtlSeconds { get; set; } = 15;

    /// <summary>Reported to client as tMaxPlayerNum; informational only, not enforced as a hard cap in M1.</summary>
    public int MaxPlayerNum { get; set; } = 1000;

    /// <summary>Legacy <c>P2ndPassword</c> (=1 in prod EU33): when true, mouse PIN is mandatory before character select.</summary>
    public bool RequireSecondPassword { get; set; } = true;
}
