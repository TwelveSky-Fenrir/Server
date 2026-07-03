using Fenrir.Contracts;
using Fenrir.Contracts.Wire;

namespace Fenrir.Network.RateLimiting;

/// <summary>
///     Hand-tuned per-opcode-class token-bucket config (architecture reference §8.5), adapted to the legacy opcode
///     set actually shipped in M1 — no Chat class yet. Deliberately NOT source-generated: these are operational
///     knobs a server operator retunes independently of the wire-format generator (Phase 2's <c>OpcodeRegistry</c>).
/// </summary>
public static class OpcodeRateLimiterPolicy
{
    /// <summary>Login/character-creation gate — expensive downstream (DB hit, session promotion), so the strictest budget.</summary>
    private static readonly (int Capacity, double TokensPerSecond) Auth = (3, 1d / 5d);

    /// <summary>Sent every tick while moving — needs a real burst allowance, not just a trickle.</summary>
    private static readonly (int Capacity, double TokensPerSecond) Movement = (10, 30d);

    /// <summary>One expected every few seconds by design; a flood is either a broken or a hostile client.</summary>
    private static readonly (int Capacity, double TokensPerSecond) Heartbeat = (2, 1d / 5d);

    /// <summary>
    ///     Everything else declared for M1 (e.g. <see cref="Opcodes.Login.Incoming.LoginKeepAlive" />,
    ///     <see cref="Opcodes.Login.Incoming.CreateAvatar" />, <see cref="Opcodes.Login.Incoming.DeleteAvatar" />,
    ///     <see cref="Opcodes.Login.Incoming.ZoneTransfer" />,
    ///     <see cref="Opcodes.Zone.Incoming.ZoneReady" />):
    ///     each fires at most a handful of times per session lifetime, so §8.5's reference burst of 3 is widened to 5
    ///     here since one bucket now covers several unrelated low-frequency opcodes instead of just one.
    /// </summary>
    private static readonly (int Capacity, double TokensPerSecond) Default = (5, 5d);

    /// <summary>
    ///     Fails fast at type-load rather than lazily on whichever opcode a live client happens to hit first:
    ///     <see cref="TokenBucket" />'s own constructor already validates capacity/rate, so touching every policy
    ///     once here surfaces a bad hand-edit of the tuples above at process startup instead of mid-session.
    /// </summary>
    static OpcodeRateLimiterPolicy()
    {
        _ = new TokenBucket(Auth.Capacity, Auth.TokensPerSecond);
        _ = new TokenBucket(Movement.Capacity, Movement.TokensPerSecond);
        _ = new TokenBucket(Heartbeat.Capacity, Heartbeat.TokensPerSecond);
        _ = new TokenBucket(Default.Capacity, Default.TokensPerSecond);
    }

    /// <summary>
    ///     Never throws for an unrecognized (server, opcode) pair — by the time this is consulted,
    ///     <c>FrameDecoder</c>/<c>OpcodeRegistry</c> have already rejected any opcode outside the real protocol, so
    ///     "not in an explicit class" just means "falls back to <see cref="Default" />".
    /// </summary>
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
