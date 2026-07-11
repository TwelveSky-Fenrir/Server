using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

/// <summary>
///     C15-regwar-reward: the real, bounded money/experience/CP/hero-rank/item-drop reward-payout half of
///     <see cref="IRegularWarEventSink" /> -- the battle-end reward loop
///     (Server/ts25zone/S07_MyGame01.cpp:5293-5413) <see cref="RegularWarSchedulerHost" /> already computes via
///     <see cref="RegularWarRewardCalculator.Compute" /> but, before this class, only ever handed to the inert
///     <c>LoggingRegularWarEventSink</c>. Every OTHER lifecycle event on this interface (countdown announce,
///     the server-160 smallest-tribe flag, active-war start, monster despawn, forced-reset disconnect) is left
///     as a deliberate no-op here -- <see cref="ZoneCenterRegularWarEventSink" /> already owns the one of those
///     that has a real Fenrir consumer (siege-state relay to center); this sink is meant to run ALONGSIDE it via
///     <c>CompositeRegularWarEventSink</c>, never in place of it (see this cluster's wiring report).
/// </summary>
/// <remarks>
///     <para>
///         <b>A4-missing-bosses (added after C15-regwar-reward shipped).</b> <see cref="OnWarConcluded" />'s
///         <paramref name="bossMonstersShouldSpawn" /> parameter -- computed by <see cref="RegularWarSchedule" />
///         as true only for a decisive server-295 win, never a draw or an empty-map abort -- previously
///         reached every registered <see cref="IRegularWarEventSink" /> including this one without ever being
///         consumed. This class now honors it by posting one <c>ZoneCommand.SummonRegularWarBoss</c> (see
///         <see cref="Zone.HandleSummonRegularWarBoss" />'s own remarks) after the reward-grant loop, matching
///         the legacy call order (Server/ts25zone/S07_MyGame01.cpp:5405-5432: map clear, win broadcast, THEN
///         the 3-call boss summon).
///     </para>
///     <para>
///         <b>Cross-thread posture.</b> <see cref="RegularWarSchedulerHost" /> calls this sink from its own
///         background-timer thread, never a zone's own tick thread -- so <see cref="OnWarConcluded" /> never
///         mutates <see cref="PlayerRuntimeState" /> directly. It resolves each grant batch's target
///         <see cref="Zone" /> via <see cref="ZoneRegistry" /> (the same map id the scheduler host itself just
///         computed the grant list from) and posts one <c>ZoneCommand.ApplyRegularWarReward</c> per grant,
///         applied on that zone's own tick thread by <c>Zone.HandleApplyRegularWarReward</c> -- the identical
///         cross-thread-command precedent <c>ZoneCommand.CreditRegularWarConclusion</c>/
///         <c>ZoneCommand.GrantValleyWarRewardDrop</c> already established for this exact host
///         (<see cref="RegularWarSchedulerHost.CreditDailyMissionAndWaterfallQuest" />).
///     </para>
///     <para>
///         <b>Bounded by construction.</b> Every field on <see cref="RegularWarRewardGrant" /> is a
///         server-computed value fed only by tick-driven state -- never a client-supplied packet field -- so
///         there is no client input to clamp here; the money overflow check and the CP zero-floor live where
///         each mutation itself happens (<c>Zone.QueueMoneyGrant</c>'s write-behind pipeline,
///         <c>Zone.GrantContributionPoints</c>'s floor), not duplicated in this sink.
///     </para>
///     <para>
///         <b>Still gated on the 12-row money table / experience-formula gap.</b> The C15 contract this class
///         implements does not supply the real 12-row money table
///         (Server/ts25zone/S07_MyGame01.cpp:7108-7171) or the 3-band <c>GetBattleExpReward</c> experience
///         formula's literal per-band coefficients (Server/ts25zone/S07_MyGame01.cpp:405-423) -- only their
///         structure (bands, divide-by-100-round-up, a 1-12 tier index) and citations, not digits. Per this
///         project's "never invent a magnitude" rule, <c>UnavailableRegularWarRewardValueProvider</c> (always
///         zero) stays the wired default; this sink applies whatever
///         <see cref="IRegularWarRewardValueProvider" /> yields either way, so a future contract supplying the
///         real literals needs no change to this class, only a new provider registration.
///     </para>
/// </remarks>
public sealed class RegularWarRewardGrantSink(ZoneRegistry zones, ILogger<RegularWarRewardGrantSink> logger)
    : IRegularWarEventSink
{
    public void OnCountdownAnnounced(short mapId, int remainingMinutes)
    {
    }

    /// <summary>
    ///     Deliberately not implemented: the C15 contract's own Open Questions §1 says the raw sort-203
    ///     "smallest present tribe" notification's client-visible effect is not recoverable from the cited
    ///     server files and should be flagged for client-side/<c>cpp-protocol-header-analyst</c> research before
    ///     assigning it any behavior, rather than guessed at. The one server-side effect this notification would
    ///     otherwise announce (the smallest-tribe kill-time +2 CP bonus, Server/ts25zone/S07_MyGame03.cpp:2676-2677)
    ///     is a separate per-kill mechanic living in the PvP kill-credit pipeline, out of this sink's own scope
    ///     (Reward: the battle-end payout loop) and unaffected by this no-op.
    /// </summary>
    public void OnSmallestTribeFlagged(short mapId, byte tribeId)
    {
    }

    public void OnActiveWarStarted(short mapId)
    {
    }

    /// <summary>
    ///     Applies every already-computed <see cref="RegularWarRewardGrant" /> by posting one
    ///     <c>ZoneCommand.ApplyRegularWarReward</c> per grant onto <paramref name="mapId" />'s own zone, then --
    ///     when <paramref name="bossMonstersShouldSpawn" /> -- posts one additional
    ///     <c>ZoneCommand.SummonRegularWarBoss</c> (see <see cref="Zone.HandleSummonRegularWarBoss" />'s own
    ///     remarks for the A4-missing-bosses Boss-561 summon this triggers). A grant for a character who has
    ///     since left the zone, or a full zone inbox, is dropped with a warning log -- same
    ///     never-blocks-never-retries posture as every other <c>Zone.Post</c> caller in this codebase; a missed
    ///     grant or a dropped boss summon here is not distinguishable from any other single-tick drop this
    ///     architecture already accepts elsewhere (e.g. a dropped Move command).
    /// </summary>
    public void OnWarConcluded(short mapId, RegularWarOutcome outcome, byte? winningTribe,
        ImmutableArray<RegularWarRewardGrant> rewards, bool bossMonstersShouldSpawn)
    {
        if (!rewards.IsDefaultOrEmpty)
        {
            if (!zones.TryGet(mapId, out var rewardZone))
            {
                logger.LogWarning(
                    "RegularWar {MapId}: {Count} reward grant(s) computed but this shard no longer hosts the map -- dropped",
                    mapId, rewards.Length);
            }
            else
            {
                foreach (var grant in rewards)
                    if (!rewardZone.Post(ZoneCommand.ApplyRegularWarReward(grant)))
                        logger.LogWarning(
                            "RegularWar {MapId}: reward grant for character {CharacterId} dropped -- zone inbox full",
                            mapId, grant.CharacterId);
            }
        }

        if (!bossMonstersShouldSpawn)
            return;

        if (!zones.TryGet(mapId, out var bossZone))
        {
            logger.LogWarning(
                "RegularWar {MapId}: boss-561 summon due but this shard no longer hosts the map -- dropped",
                mapId);
            return;
        }

        if (!bossZone.Post(ZoneCommand.SummonRegularWarBoss()))
            logger.LogWarning("RegularWar {MapId}: boss-561 summon command dropped -- zone inbox full", mapId);
    }

    public void OnMonstersShouldDespawn(short mapId)
    {
    }

    public void OnAllSessionsShouldDisconnect(short mapId)
    {
    }
}
