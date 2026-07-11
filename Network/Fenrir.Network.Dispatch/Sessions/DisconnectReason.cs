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

    TimedZoneExpired,

    GmCommandLogout,

    GmKicked,

    ValleyWarForcedReset,

    WardrobeFull,

    LabyrinthMissionEnded
}
