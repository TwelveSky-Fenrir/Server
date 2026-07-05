using Fenrir.Contracts;
using Fenrir.Network.Abstractions;

namespace Fenrir.Network.RateLimiting;

// Hand-tuned per-opcode-class budgets not source-generated since operators retune these independently of OpcodeRegistry.
public static class OpcodeRateLimiterPolicy
{
    /// <summary>Login/character-creation gate — expensive downstream (DB hit, session promotion), so the strictest budget.</summary>
    private static readonly (int Capacity, double TokensPerSecond) Auth = (3, 1d / 5d);

    /// <summary>Sent every tick while moving — needs a real burst allowance, not just a trickle.</summary>
    private static readonly (int Capacity, double TokensPerSecond) Movement = (10, 30d);

    /// <summary>One expected every few seconds by design; a flood is either a broken or a hostile client.</summary>
    private static readonly (int Capacity, double TokensPerSecond) Heartbeat = (2, 1d / 5d);

    /// <summary>
    ///     Everything else in the reference burst of 3 is widened to 5 since one bucket covers several
    ///     low-frequency opcodes.
    /// </summary>
    private static readonly (int Capacity, double TokensPerSecond) Default = (5, 5d);

    // Touches every policy at type-load so a bad hand-edited tuple fails at startup, not mid-session.
    static OpcodeRateLimiterPolicy()
    {
        _ = new TokenBucket(Auth.Capacity, Auth.TokensPerSecond);
        _ = new TokenBucket(Movement.Capacity, Movement.TokensPerSecond);
        _ = new TokenBucket(Heartbeat.Capacity, Heartbeat.TokensPerSecond);
        _ = new TokenBucket(Default.Capacity, Default.TokensPerSecond);
    }

    // Never throws: an unrecognized (server, opcode) just falls back to Default — FrameDecoder already
    // rejected anything outside the real protocol.
    public static (int Capacity, double TokensPerSecond) PolicyFor(FenrirServer server, byte opcode)
    {
        return (server, opcode) switch
        {
            (FenrirServer.Login, Opcodes.Login.Incoming.Login) => Auth,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.ZoneHandshake) => Auth,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.EnterWorld) => Auth,

            (FenrirServer.Zone, Opcodes.Zone.Incoming.AvatarAction) => Movement,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.AvatarActionResume) => Movement,

            (FenrirServer.Zone, Opcodes.Zone.Incoming.Heartbeat) => Heartbeat,

            _ => Default
        };
    }
}
