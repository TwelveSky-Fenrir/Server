namespace Fenrir.Domain.Login;

public sealed class LoginServerOptions
{
    public int Port { get; set; } = 29998;

    /// <summary>
    ///     Base d'adressage des zones (legacy <c>1100 + N</c>). Le Login route un client vers une map en renvoyant
    ///     <c>ZoneBasePort + mapId</c> comme port de destination (le client (re)connecte par zone — Décision A /
    ///     doc <c>03_Topologie_TCP_et_Aspire.md</c> §3). DOIT correspondre à <c>GameServerOptions.ZoneBasePort</c>
    ///     (le GameServer binde ses listeners sur exactement ces ports).
    /// </summary>
    public int ZoneBasePort { get; set; } = 1100;

    public int ExpectedClientVersion { get; set; } = 90354;

    public int TicketTtlSeconds { get; set; } = 15;

    public int ShardReachabilityProbeTimeoutMilliseconds { get; set; } = 750;

    public bool RequireSecondPassword { get; set; } = true;

    public int AccountSessionRefreshIntervalSeconds { get; set; } = 60;

    public int MaxConnectionsPerIp { get; set; } = 40;

    public int MaxProtocolViolationsPerIpPerHour { get; set; } = 30;

    public int IdleSweepIntervalSeconds { get; set; } = 1;

    public bool OnlyAdminCanLogin { get; set; }
}
