using Fenrir.Network.Abstractions;

namespace Fenrir.Network.Dispatch.RateLimiting;

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
    ///     CZ_PROCESS_ATTACK_SEND (Zone op18) -- network-layer defense-in-depth only, NOT a replacement for
    ///     <see cref="Fenrir.Application.Game.Domain.Combat.AttackPacketBudget" />, which remains the real
    ///     per-action anti-cheat gate (that check runs on the tick thread against the accepting character's
    ///     <c>AttackSubPacketCeiling</c>, set per accepted CZ_AVATAR_ACTION_SEND by
    ///     <c>CharacterMotionWhitelist</c>). Capacity 8 gives 3 tokens of headroom above the highest ceiling
    ///     value in that whitelist's table (5, the max legitimate sub-packet burst for one accepted multi-hit
    ///     attack/skill action), so a genuine multi-hit combo is never throttled here before
    ///     <c>AttackPacketBudget</c> even sees it. The 4/sec refill is deliberately tighter than
    ///     <see cref="Default" />'s flat 5/sec: PROCESS_ATTACK_SEND is one of the most expensive incoming
    ///     opcodes per accepted packet (RNG combat resolution, HP mutation, AOI rebroadcast, potential
    ///     write-behind persistence), so a client blasting raw op18 frames as fast as the socket allows should
    ///     be capped harder than the generic low-frequency opcodes sharing <see cref="Default" />, while 4/sec
    ///     sustained still comfortably covers routine single-hit auto-attacks plus a full 5-hit combo roughly
    ///     every 1.25s indefinitely once the initial burst is spent.
    /// </summary>
    private static readonly (int Capacity, double TokensPerSecond) Attack = (8, 4d);

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
        _ = new TokenBucket(Attack.Capacity, Attack.TokensPerSecond);
        _ = new TokenBucket(Default.Capacity, Default.TokensPerSecond);
    }

    // Never throws: an unrecognized (server, opcode) just falls back to Default — FrameDecoder already
    // rejected anything outside the real protocol.
    public static (int Capacity, double TokensPerSecond) PolicyFor(FenrirServer server, byte opcode)
    {
        return (server, opcode) switch
        {
            (FenrirServer.Login, Opcodes.Login.Incoming.Loggedin) => Auth,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.ZoneHandshake) => Auth,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.EnterWorld) => Auth,

            (FenrirServer.Zone, Opcodes.Zone.Incoming.AvatarAction) => Movement,
            (FenrirServer.Zone, Opcodes.Zone.Incoming.AvatarActionResume) => Movement,

            (FenrirServer.Zone, Opcodes.Zone.Incoming.Heartbeat) => Heartbeat,

            // GM-BLOCK (tSort 519) rides the shared GenericAction envelope (opcode 19) alongside every other
            // ProcessForData sub-command -- there is no dedicated GM-tier opcode to gate independently of that
            // envelope's own budget (Default, below), since legacy itself never assigned GM-BLOCK a distinct
            // wire opcode. See GenericActionHandler's own tSort 519 branch.

            (FenrirServer.Zone, Opcodes.Zone.Incoming.Attack) => Attack,

            _ => Default
        };
    }
}
