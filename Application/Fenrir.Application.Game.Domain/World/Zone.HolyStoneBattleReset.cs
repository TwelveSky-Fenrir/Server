using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Zone037's HSB (Holy Stone Battle) cluster-wide rank reset -- the per-zone, per-player half of the
///     code-40 broadcast handler (Server/ts25zone/S07_MyGame08.cpp:223-271, C15-hsb-reset behavior contract).
///     Posted once per zone, every time the Tribe Symbol Battle window opens
///     (<see cref="ZoneWar.ZoneEventBroadcaster.AnnounceTribeSymbolBattleStarted" /> and its cross-shard replay
///     twin, <see cref="ZoneWar.ZoneEventBroadcaster.ApplyRelayedEvent" />'s case 40 -- see wiringManifest for
///     the exact call added to each) -- same "payload-less trigger, draining one means sweep every currently
///     connected player once" shape as <see cref="ApplyHeroRankingRolloverReset" />, this class's closest
///     sibling.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame08.cpp:223-271 (the full code-40 handler: pre-loop spawn/guard/
///     eviction/date steps, the ready-state skip, the transferring-player branch and its two-store asymmetry,
///     the main branch's two-store rank-point clear, always-sent date notification, and conditional buff-clear
///     with stat recompute) ; Server/ts25zone/S05_MyTransfer.cpp:519-542 (the avatar-change-info notification
///     wire -- three 16-bit values under <c>USE_PREMIUM_LONGTIME</c>, the player-targeted overload) ;
///     Server/Header/Protocol/STRUCT.h:1580-1582 (sub-command codes: rank-point=66, rank-point-date=67,
///     rank-buff=68) ; Server/Header/Protocol/DEFINE.h:77 (<c>USE_RANK_POINT</c> commented out at its sole
///     declaration, defined nowhere under <c>Server/</c> -- why <see cref="PlayerRuntimeState.RankPoint" /> is
///     always 0 in production, so the sub-command-66 notification never actually fires in the shipped build;
///     the reset itself is still live, reachable code operating on an always-zero value, not dead code).
///     <para>
///         GAP -- not modeled, deliberately not guessed, both flagged unrecoverable-this-session by the
///         C15-hsb-reset contract: (1) the tribe-symbol-stone spawn routine and the guard-spawn routine (steps
///         1-2 of the same pre-loop, S07_MyGame08.cpp:229-230) are NOT reproduced here -- they are already
///         live production behavior via <see cref="ZoneWar.ZoneEventBroadcaster.AnnounceTribeSymbolBattleStarted" />'s
///         existing <c>symbolSpawner.EvaluateNow</c>/<c>guardSpawner.ForceOrdinaryResummon</c> calls, so
///         reproducing them a second time here would double-apply them; (2) the HSB-countdown eviction worker
///         (step 3, call site S07_MyGame08.cpp:231) is NOT invoked from here -- its internals (source finding:
///         Server/ts25zone/S07_MyGame03.cpp:9066-9130, including a server-number-&gt;required-tribe table) were
///         not independently reopened this session, and this file deliberately does NOT assume it is the same
///         routine as the structurally-similar but differently-cited
///         <see cref="ZoneWar.HolyStoneTerritoryEvictionSweep" />
///         (its own citation, S07_MyGame01.cpp:4289-4342, is a distinct, already-repeating ~63s sweep). Wiring
///         step 3 needs a fresh legacy-behavior-translator finding confirming (or refuting) that equivalence
///         before it can be implemented for real -- see <see cref="ApplyHolyStoneBattleRankReset" />'s own
///         log line marking the gap.
///     </para>
/// </remarks>
public sealed partial class Zone
{
    /// <summary>
    ///     Generous, rarely-touched capacity for <see cref="_holyStoneBattleRankResetInbox" /> -- same rationale
    ///     as <see cref="HeroRankingRolloverInboxCapacity" />: the trigger (Zone037's fixed-hour, day-gated HSB
    ///     schedule, <see cref="ZoneWar.TribeSymbolBattleScheduler" />) can fire at most a handful of times a day.
    /// </summary>
    private const int HolyStoneBattleRankResetInboxCapacity = 4;

    /// <summary>
    ///     Per-tick drain cap for <see cref="_holyStoneBattleRankResetInbox" /> -- see
    ///     <see cref="InboxDrainCapPerTick" />'s own remarks.
    /// </summary>
    private const int HolyStoneBattleRankResetInboxDrainCapPerTick = HolyStoneBattleRankResetInboxCapacity / 2;

    /// <summary>Legacy <c>B_AVATAR_CHANGE_INFO_2</c> sub-command 66 -- rank-point cleared (STRUCT.h:1580).</summary>
    private const int RankPointClearedStatSort = 66;

    /// <summary>Legacy sub-command 67 -- rank-point-date reset, always sent (STRUCT.h:1581).</summary>
    private const int RankPointDateResetStatSort = 67;

    /// <summary>Legacy sub-command 68 -- rank-buff cleared (STRUCT.h:1582).</summary>
    private const int RankBuffClearedStatSort = 68;

    /// <summary>
    ///     Cluster-wide HSB rank-reset trigger -- posted once per zone every time the Tribe Symbol Battle
    ///     window opens. Carries no per-instance data: draining one just means "sweep every currently connected
    ///     player once," see <see cref="ApplyHolyStoneBattleRankReset" />.
    /// </summary>
    private readonly Channel<HolyStoneBattleRankResetZoneCommand> _holyStoneBattleRankResetInbox =
        Channel.CreateBounded<HolyStoneBattleRankResetZoneCommand>(
            new BoundedChannelOptions(HolyStoneBattleRankResetInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Posted once per Zone037 HSB battle-open event -- see <see cref="ApplyHolyStoneBattleRankReset" />.
    /// </summary>
    public bool PostHolyStoneBattleRankReset()
    {
        return _holyStoneBattleRankResetInbox.Writer.TryWrite(default);
    }

    private void DrainHolyStoneBattleRankResetCommands()
    {
        var processed = 0;
        while (processed < HolyStoneBattleRankResetInboxDrainCapPerTick &&
               _holyStoneBattleRankResetInbox.Reader.TryRead(out _))
        {
            processed++;
            try
            {
                ApplyHolyStoneBattleRankReset();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} HSB cluster-wide rank reset failed", MapId);
            }
        }

        if (processed >= HolyStoneBattleRankResetInboxDrainCapPerTick)
            LogDrainCapEngaged(_holyStoneBattleRankResetInbox.Reader, "holy-stone-battle-rank-reset",
                HolyStoneBattleRankResetInboxDrainCapPerTick);
    }

    /// <summary>
    ///     Pre-loop steps 3-4 (steps 1-2, symbol/guard spawn, are already applied by the caller -- see this
    ///     file's own class remarks) followed by the per-player reset itself: every currently connected
    ///     character on THIS zone (both the transferring branch and the main, in-world branch -- both appear in
    ///     <see cref="_players" />, matching every other server-wide sweep in this codebase, e.g.
    ///     <see cref="ZoneWar.HolyStoneWarCycle.AdvanceQuestProgressServerWide" />) has its HSB rank state reset.
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S07_MyGame08.cpp:231-271. See this file's own class remarks for GAP (2).</remarks>
    private void ApplyHolyStoneBattleRankReset()
    {
        // GAP: step 3, the HSB-countdown eviction worker (S07_MyGame08.cpp:231) -- not modeled, see class
        // remarks. Left as an explicit marker rather than silently doing nothing.
        logger.LogDebug(
            "Zone {MapId} HSB rank reset: HSB-countdown eviction worker (S07_MyGame08.cpp:231) not modeled -- see C15-hsb-reset contract Open Questions",
            MapId);

        var today = GameDate.Today();

        foreach (var state in _players.Values)
        {
            if (state.IsMovingZone)
            {
                // Transferring-player branch (S07_MyGame08.cpp:243-252): only the in-memory avatar copy is
                // touched, and only if the stored date is not already today -- no notification, no persistent
                // store write, no stat recompute. A character whose date already reads today is left
                // completely untouched (not even re-stamped).
                if (state.RankPointDate == today)
                    continue;

                state.RankPointDate = today;
                state.RankPoint = 0;
                state.RankBuffType = 0;
                continue;
            }

            // Main branch (S07_MyGame08.cpp:254-269), in order:
            // 1. Rank point -> 0, sub-command 66 sent first only if it was above zero (always false in
            //    production -- see PlayerRuntimeState.RankPoint's own remarks -- but modeled for real so a
            //    future USE_RANK_POINT-equivalent accumulator needs no change here).
            if (state.RankPoint > 0)
                state.Session.Send(new AvatarStatUpdateResponse
                    { Sort = RankPointClearedStatSort, Value = 0, Value2 = 0 });
            state.RankPoint = 0;

            // 2. Rank-point date -> today, sub-command 67 ALWAYS sent (the only unconditionally observable
            //    effect of this reset in production, alongside the buff-clear below).
            state.RankPointDate = today;
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = RankPointDateResetStatSort, Value = today, Value2 = 0 });

            // 3. Persistent rank-point store -> 0 (S07_MyGame08.cpp:263, the second of the two legacy
            //    storage locations). No DB column exists for either store yet (see
            //    PlayerRuntimeState.RankPoint's own remarks) -- step 1 above already zeroed the one property
            //    Fenrir has, so there is nothing further to write here until a real schema lands.

            // 4. Rank buff, only if currently active -- the one part of this reset with a genuine, live
            //    combat effect (the buff is read unconditionally by MyFactor.cpp's per-tier formulas).
            if (state.RankBuffType != 0)
            {
                state.RankBuffType = 0;
                state.Session.Send(new AvatarStatUpdateResponse
                    { Sort = RankBuffClearedStatSort, Value = 0, Value2 = 0 });

                var changedSlots = state.BuffChangeScratch;
                Array.Clear(changedSlots);
                RecomputeStatsAndBroadcastBuffs(state, changedSlots);
            }
        }
    }
}

/// <summary>
///     Payload-less HSB cluster-wide rank-reset trigger -- draining one means "reset every currently connected
///     player's HSB rank state on THIS zone," see <see cref="Zone.ApplyHolyStoneBattleRankReset" />.
/// </summary>
public readonly record struct HolyStoneBattleRankResetZoneCommand;
