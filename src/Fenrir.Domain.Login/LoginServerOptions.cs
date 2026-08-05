namespace Fenrir.Domain.Login;

public sealed class LoginServerOptions
{
    public int Port { get; set; } = 29998;

    public int ZoneBasePort { get; set; } = 1100;

    public int ExpectedClientVersion { get; set; } = 90354;

    public int TicketTtlSeconds { get; set; } = 60;

    public int ShardReachabilityProbeTimeoutMilliseconds { get; set; } = 750;

    public bool RequireSecondPassword { get; set; } = true;

    public int AccountSessionRefreshIntervalSeconds { get; set; } = 60;

    public int MaxConnectionsPerIp { get; set; } = 512;

    public int MaxProtocolViolationsPerIpPerHour { get; set; } = 1_000;

    public int IdleSweepIntervalSeconds { get; set; } = 1;

    public int PreAuthenticationHandshakeTimeoutSeconds { get; set; } = 10;

    public bool OnlyAdminCanLogin { get; set; }
}
