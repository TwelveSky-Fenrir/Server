namespace Fenrir.Network.Sessions;

/// <summary>Why a session was torn down — exported as a metric tag, never exposed to the client.</summary>
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
    Faulted
}
