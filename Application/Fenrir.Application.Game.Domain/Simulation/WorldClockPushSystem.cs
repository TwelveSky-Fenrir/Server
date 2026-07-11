using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     A6 -- self-throttled per-session "current time" push, reproducing legacy's <c>MyUtil::SendTime</c>
///     (Server/ts25zone/S07_MyGame03.cpp:9685-9714), called unconditionally (no build-macro gate) once per
///     legacy tick for every ready avatar from <c>AVATAR_OBJECT::Update</c>
///     (Server/ts25zone/S07_MyGame04.cpp:887, itself driven by the per-avatar tick loop at
///     Server/ts25zone/S07_MyGame01.cpp:2031-2038). Sends the owning client only a single
///     <see cref="AvatarStatUpdateResponse" /> (legacy <c>ZC_AVATAR_CHANGE_INFO_2</c>, opcode
///     <c>ZCP_AVATAR_CHANGE_INFO_2</c> = 22, Server/Header/Protocol/ZONE.h:453-460,1435) with
///     <see cref="NowTimeClassificationSort" /> and a single packed decimal number encoding
///     month(0-based)/day/hour(0-23)/minute(0-59)/day-of-week(0-6, Sunday=0) -- never a broadcast, never
///     combined with any AOI-interest path (A6-sendtime-push contract, Outputs).
/// </summary>
/// <remarks>
///     <para>
///         <b>Cadence.</b> Registered as an <see cref="ISimulationSystem" /> singleton
///         (<see cref="ISimulationSystem.Simulate" /> runs once per <see cref="Zone" /> tick for every whole
///         500 ms legacy tick that elapsed, and is skipped entirely when none did) -- the same self-contained,
///         order-independent posture as <c>PlayTimeAccrualSystem</c>/<c>CashCatalogStaleNotifySystem</c> (see
///         those systems' own remarks): this class only reads/writes its own
///         <see cref="PlayerRuntimeState.WorldClockPushThrottleState" /> field and sends to its own session, so
///         its position among the other registered systems does not matter.
///     </para>
///     <para>
///         <b>Throttle state machine</b> (re-derived verbatim from <c>MyUtil::SendTime</c>, contract "Side
///         effects" section). The wall-clock second-of-minute is sampled once per <see cref="Simulate" /> call
///         -- shared by every player processed in that same call, exactly matching legacy's own
///         once-per-tick <c>UpdateServerTime()</c> refresh (S07_MyGame01.cpp:1739-1745,84-93) -- and each
///         player's own <see cref="PlayerRuntimeState.WorldClockPushThrottleState" /> is advanced
///         independently:
///         <list type="number">
///             <item>
///                 If the field is currently <see cref="ThrottleStateBlocked" /> and the sampled second is
///                 below <see cref="RearmSecondThreshold" />, move to <see cref="ThrottleStateRearmPending" />.
///             </item>
///             <item>
///                 Independently of step 1 (the two conditions are mutually exclusive, so this never actually
///                 overrides step 1's own transition in practice, exactly as the contract notes): if the
///                 sampled second is at or above <see cref="BlockSecondThreshold" />, move to
///                 <see cref="ThrottleStateBlocked" />.
///             </item>
///             <item>
///                 If the field is now <see cref="ThrottleStateBlocked" /> or <see cref="ThrottleStateJustSent" />,
///                 stop -- no transmission this call (the mutation from steps 1/2, if any, is still kept).
///                 Otherwise (<see cref="PlayerRuntimeState.WorldClockPushThrottleState" /> was
///                 <c>0</c>/unset or <see cref="ThrottleStateRearmPending" />) advance to
///                 <see cref="ThrottleStateJustSent" /> and transmit.
///             </item>
///         </list>
///         Net effect: a session whose state has settled into the steady blocked/just-sent cycle receives the
///         push on the first tick of each real minute where the sampled second lands in [0, 2) -- roughly a
///         two-second window -- and nothing on every other tick of that minute. Under tick lag, a minute whose
///         [0, 2) window is skipped entirely (the clock jumps straight past it) silently loses that minute's
///         push; this is self-correcting the following minute and raises no error (contract Edge case 1).
///     </para>
///     <para>
///         <b>Forced path, not wired here.</b> <c>MyUtil::SendTime(user, TRUE)</c>'s only other call site is
///         the zone-entry/avatar-registration handler (<c>CZ_REGISTER_AVATAR_SEND</c>,
///         Server/ts25zone/S04_MyWork02.cpp:1110), which skips the state machine above entirely and
///         unconditionally sends. <see cref="SendForced" /> reproduces that half; wiring it into Fenrir's own
///         world-entry handler is deliberately left to a separate integration pass (see this sub-mechanic's
///         own wiring manifest) rather than edited into an existing handler here. The legacy caller also
///         resets the throttle-state field to 0 immediately before its own forced call
///         (S04_MyWork02.cpp:1108) -- a distinct, caller-side effect the contract itself notes is "not one
///         this behavior performs itself" (A6-sendtime-push contract, Side effects) and is, in any case,
///         functionally inert here since <see cref="SendForced" /> always overwrites the field to
///         <see cref="ThrottleStateJustSent" /> regardless of its prior value -- so no separate reset method is
///         exposed.
///     </para>
///     <para>
///         <b>Time source and a deliberate, documented divergence from legacy's non-atomic sampling.</b> The
///         legacy implementation samples each of month/day/hour/minute/weekday via five independent calls,
///         each re-reading the wall clock (Server/Header/datetime.h:38-43,69-75) -- a sub-second race at a
///         minute/hour/day boundary could in principle produce an internally inconsistent packed value. The
///         contract leaves "preserve the non-atomicity or replace it with a single atomic read" as an
///         explicitly open, undecided question (contract Edge case 3). This implementation takes a single
///         atomic <see cref="TimeProvider.GetUtcNow" /> snapshot per <see cref="Simulate" /> call instead --
///         the idiomatic, strictly-more-correct choice for a from-scratch reimplementation, and the same
///         choice this class's payload-encoding helper (<see cref="EncodePackedTime" />) depends on for a
///         single consistent five-field tuple. UTC (not host-local time) is used specifically so this
///         system's output does not depend on the deploying container's timezone configuration -- the
///         legacy source's own accessors' UTC-vs-local behavior was not independently confirmed this session
///         (not covered by the contract's own citations), so this is a Fenrir-side implementation choice, not
///         a reproduced legacy fact; flagged in this sub-mechanic's open questions.
///     </para>
/// </remarks>
public sealed class WorldClockPushSystem(TimeProvider? timeProvider = null) : ISimulationSystem
{
    /// <summary>
    ///     AvatarStatUpdateResponse.Sort for S098NOW_TIME_GM_MESSAGE (Server/Header/Protocol/STRUCT.h:1610).
    /// </summary>
    public const int NowTimeClassificationSort = 98;

    /// <summary>Legacy literal 0 -- no per-tick evaluation has advanced this field yet.</summary>
    public const int ThrottleStateUnset = 0;

    /// <summary>Legacy literal 1 -- the field was "blocked" but the wall clock has rolled into a new minute's rearm window.</summary>
    public const int ThrottleStateRearmPending = 1;

    /// <summary>Legacy literal 2 -- past this minute's send window; no further transmission until the next minute's rearm.</summary>
    public const int ThrottleStateBlocked = 2;

    /// <summary>Legacy literal 3 -- already transmitted for the current rearm window.</summary>
    public const int ThrottleStateJustSent = 3;

    /// <summary>Sampled second-of-minute strictly below this value is eligible to rearm a blocked state.</summary>
    private const int RearmSecondThreshold = 2;

    /// <summary>Sampled second-of-minute at or above this value forces the blocked state.</summary>
    private const int BlockSecondThreshold = 3;

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var now = _timeProvider.GetUtcNow();
        var secondOfMinute = now.Second;
        var packet = BuildPacket(now);

        foreach (var state in zone.Players)
        {
            var throttleState = state.WorldClockPushThrottleState;

            if (throttleState == ThrottleStateBlocked && secondOfMinute < RearmSecondThreshold)
                throttleState = ThrottleStateRearmPending;

            if (secondOfMinute >= BlockSecondThreshold)
                throttleState = ThrottleStateBlocked;

            if (throttleState is ThrottleStateBlocked or ThrottleStateJustSent)
            {
                state.WorldClockPushThrottleState = throttleState;
                continue;
            }

            state.WorldClockPushThrottleState = ThrottleStateJustSent;
            state.Session.Send(packet);
        }
    }

    /// <summary>
    ///     Reproduces the forced call path (<c>MyUtil::SendTime(user, TRUE)</c>) -- unconditionally advances
    ///     <paramref name="state" />'s throttle field to <see cref="ThrottleStateJustSent" /> and transmits,
    ///     skipping the state-machine checks in <see cref="Simulate" /> entirely. Not yet called from
    ///     anywhere in Fenrir; see this class's own remarks and this sub-mechanic's wiring manifest for the
    ///     intended zone-entry/registration call site.
    /// </summary>
    public void SendForced(PlayerRuntimeState state)
    {
        state.WorldClockPushThrottleState = ThrottleStateJustSent;
        state.Session.Send(BuildPacket(_timeProvider.GetUtcNow()));
    }

    /// <summary>
    ///     Packs month(0-based)/day/hour(0-23)/minute(0-59)/day-of-week(0-6, Sunday=0) into a single decimal
    ///     integer, each field's decimal-place weight equivalent to legacy's own "two digits per field except
    ///     a single-digit day-of-week, concatenated with no separators" encoding (A6-sendtime-push contract,
    ///     Outputs) -- e.g. month=0/day=5/hour=14/minute=7/dow=3 packs to the same digit sequence as the
    ///     zero-padded string "00" + "05" + "14" + "07" + "3".
    /// </summary>
    public static int EncodePackedTime(DateTimeOffset now)
    {
        var monthZeroBased = now.Month - 1;
        return monthZeroBased * 10_000_000 + now.Day * 100_000 + now.Hour * 1_000 + now.Minute * 10 +
               (int)now.DayOfWeek;
    }

    /// <summary>
    ///     Builds the outgoing packet for a given instant -- <see cref="AvatarStatUpdateResponse.Value2" /> is
    ///     always 0 for this classification value (the call site only ever supplies two of the three logical
    ///     values; the unfilled third slot defaults to zero, A6-sendtime-push contract Outputs).
    /// </summary>
    public static AvatarStatUpdateResponse BuildPacket(DateTimeOffset now)
    {
        return new AvatarStatUpdateResponse
        {
            Sort = NowTimeClassificationSort,
            Value = EncodePackedTime(now),
            Value2 = 0
        };
    }
}
