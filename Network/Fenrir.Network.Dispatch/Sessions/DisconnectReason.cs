namespace Fenrir.Network.Dispatch.Sessions;

public enum DisconnectReason
{
    ClientClosed,
    Malformed,
    UnknownOpcode,
    StateViolation,
    RateLimited,
    SlowConsumer,
    ServerShutdown,
    Evicted,
    Faulted,

    IpBlocked,

    Banned,

    IdleTimeout,

    ProcessingFault,

    SendBufferOverflow,

    TimedZoneExpired,

    GmCommandLogout,

    GmKicked,

    ValleyWarForcedReset,

    WardrobeFull,

    LabyrinthMissionEnded,

    ContributionPointExchangeRetired
}
