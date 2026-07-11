using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The client-notifying, per-zone-tick driver for <see cref="ValleyWarSchedule" /> (Zone 200/297/298/299,
///     "Valley of the Deceased") -- a genuine <see cref="ISimulationSystem" /> (not a separate Hosting-level
///     background timer, unlike <see cref="Fenrir.Application.Game.Hosting.World.ZoneWar.RegularWarSchedulerHost" />
///     for the unrelated Zone049 family) since every side effect this contract calls for (op94 cluster/shard-wide
///     broadcast, zone-local opcode 116/118 sends, ground-item drops, forced session disconnects) can run
///     directly on the affected zone's own tick thread -- same shape as <see cref="Simulation.HoisundoCountdownSystem" />.
/// </summary>
/// <remarks>
///     <para>
///         <b>
///             Documented gaps, not silent ones -- two pieces of concrete legacy data are still unavailable in
///             the source behavior contract this ports, so the corresponding side effects are logged, not
///             fabricated:
///         </b>
///     </para>
///     <list type="bullet">
///         <item>
///             The general kill-race monster summon (contract §5, side effect 4): no monster id, count, or spawn
///             coordinate is given ("an unlimited/untimed monster summon is triggered... unconditionally").
///         </item>
///         <item>
///             The "animal5" randomized boss-win reward item (contract §8b, one of eight drops) has no resolved
///             concrete item id -- see <see cref="Zone.HandleGrantValleyWarRewardDrop" />'s own remarks for the
///             other 7, which ARE granted for real.
///         </item>
///     </list>
///     <para>
///         The kill-race-win boss summon itself (contract §6b, monster id 756 =
///         <see cref="ValleyWarSchedule.BossMonsterId" />) is NOT a gap: its fixed coordinate is available (
///         <see cref="Zone200GateBreachBossCatalog.SummonX" />/<c>SummonY</c>/<c>SummonZ</c>) and this class calls
///         <see cref="Zone.HandleSummonValleyWarBoss" /> directly on <see cref="React" />'s <c>TribeWin</c> branch.
///     </para>
///     <para>
///         A direct consequence of <see cref="ValleyWarEnvironmentSnapshot.BossSlotOccupied" /> always being
///         reported <see langword="false" /> here (see that snapshot parameter's own remarks -- a SEPARATE,
///         still-open gap: nothing wires the summoned boss's live/dead status back into the environment
///         snapshot): <see cref="ValleyWarSchedule.BossWindow" /> still always resolves as "boss already dead" on
///         its first tick, even though the boss is now genuinely summoned as a client-visible monster.
///     </para>
///     <para>
///         <b>Reward-grant scope.</b> The source contract's §8b eligibility ("every member of the winning tribe
///         who is currently online... at that player's current location") is NOT scoped to the war map itself --
///         it reaches every winning-tribe character tracked ANYWHERE on this shard. <see cref="GrantRewardsToTribe" />
///         therefore posts a <see cref="ZoneCommand.GrantValleyWarRewardDrop" /> to whichever zone currently hosts
///         each eligible character (via <see cref="Lazy{T}" />-wrapped <see cref="ZoneRegistry" />, the same
///         circular-DI-avoidance shape <see cref="Monsters.MonsterSpawnScheduler" />'s own remarks document for
///         its <see cref="ZoneEventBroadcaster" /> dependency -- this class is, likewise, one of the
///         <see cref="ISimulationSystem" /> instances <see cref="ZoneRegistry" /> itself resolves at construction
///         time) rather than calling <c>Zone.SpawnGroundItem</c> directly on a zone this system's own
///         <see cref="Simulate" /> call did not receive -- that method is tick-owned-caller-only, and a foreign
///         zone's own tick thread could be concurrently mutating it.
///     </para>
/// </remarks>
public sealed class ValleyWarSystem(
    ValleyWarKillRegistry killRegistry,
    Lazy<ZoneEventBroadcaster> broadcaster,
    Lazy<ZoneRegistry> zoneRegistry,
    ILogger<ValleyWarSystem> logger) : ISimulationSystem
{
    /// <summary>Zone-local door-pending countdown context tag (contract §11: "0,0 for the door-open countdown").</summary>
    private const int DoorContextTag = 0;

    /// <summary>
    ///     Zone-local kill-race/boss-window countdown context tag (contract §11: "1,1 for the kill-race and
    ///     boss-window countdowns -- the two later contexts are not distinguished from each other").
    /// </summary>
    private const int RaceOrBossContextTag = 1;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        if (!ValleyWarMapCatalog.Contains(zone.MapId) || legacyTicksElapsed <= 0)
            return;

        var schedule = killRegistry.GetOrCreate(zone.MapId);

        for (var i = 0; i < legacyTicksElapsed; i++)
        {
            var snapshot = BuildSnapshot(zone);
            var result = schedule.Tick(snapshot);
            React(zone, schedule, result);
        }
    }

    private static ValleyWarEnvironmentSnapshot BuildSnapshot(Zone zone)
    {
        var eligiblePresent = false;
        foreach (var player in zone.Players)
        {
            if (player.IsMovingZone)
                continue;

            eligiblePresent = true;
            break;
        }

        // BossSlotOccupied always false -- see this class's own remarks and ValleyWarEnvironmentSnapshot's.
        return new ValleyWarEnvironmentSnapshot(eligiblePresent);
    }

    private void React(Zone zone, ValleyWarSchedule schedule, ValleyWarTickResult result)
    {
        if (result.GateCountdownValue is { } remaining)
            broadcaster.Value.AnnounceValleyWarGateCountdown(remaining);

        if (result.GateOpened)
            broadcaster.Value.AnnounceValleyWarGateOpened();

        if (result.GateClosed)
            broadcaster.Value.AnnounceValleyWarGateClosed();

        if (result.DoorCountdownValue is { } doorCountdown)
            SendZoneLocalCountdown(zone, DoorContextTag, doorCountdown);

        if (result.DoorOpened)
        {
            broadcaster.Value.AnnounceValleyWarDoorOpened();
            LogMonsterSummonGap(zone,
                "the general kill-race monster population (no id/count/coordinate data available)");
        }

        if (result.KillRaceQuotas is { } quotas)
            SendZoneLocalQuotas(zone, quotas);

        if (result.KillRaceCountdownValue is { } killRaceCountdown)
            SendZoneLocalCountdown(zone, RaceOrBossContextTag, killRaceCountdown);

        if (result.KillRaceEndedEmptyOrTimeout)
            broadcaster.Value.AnnounceValleyWarReturnToTown();

        if (result.TribeWin && result.WinningTribe is { } winner)
        {
            broadcaster.Value.AnnounceValleyWarTribeWin(winner);

            // Boss-756's fixed summon coordinate IS available (Zone200GateBreachBossCatalog.SummonX/Y/Z,
            // re-derived byte-for-byte identical from Server/ts25zone/S07_MyGame01.cpp:11413-11416) -- unlike
            // the general kill-race population gap logged below, this is no longer a documented gap. This class
            // is itself a genuine ISimulationSystem already running on THIS zone's own tick thread (unlike
            // RegularWarSchedulerHost's background-timer thread for the sibling Boss-561 family), so the summon
            // primitive is called directly, with no ZoneCommand/Post hop needed.
            zone.HandleSummonValleyWarBoss();
        }

        if (result.BattleScrollDeleted)
            broadcaster.Value.AnnounceValleyWarBattleScrollDeleted();

        if (result.BossWindowCountdownValue is { } bossCountdown)
            SendZoneLocalCountdown(zone, RaceOrBossContextTag, bossCountdown);

        if (result.BossWindowTimeout)
            broadcaster.Value.AnnounceValleyWarReturnToTown();

        if (result.BossWin)
        {
            broadcaster.Value.AnnounceValleyWarBossDefeated();
            if (schedule.WinningTribe is { } winningTribe)
                GrantRewardsToTribe(winningTribe);
        }

        if (result.PostWinReturnToTown)
            broadcaster.Value.AnnounceValleyWarReturnToTown();

        if (result.AllSessionsShouldDisconnect)
            DisconnectEveryone(zone);
    }

    /// <summary>Opcode 116 (contract §11) -- zone-local only, ready + not-mid-transfer recipients.</summary>
    private static void SendZoneLocalCountdown(Zone zone, int contextTag, int value)
    {
        var packet = new ZoneWar297StatusResponse { Value00 = contextTag, Value01 = contextTag, Value02 = value };
        foreach (var player in zone.Players)
            if (!player.IsMovingZone)
                player.Session.Send(packet);
    }

    /// <summary>Opcode 118 (contract §12) -- zone-local only, ready + not-mid-transfer recipients.</summary>
    private static void SendZoneLocalQuotas(Zone zone, ImmutableArray<int> quotas)
    {
        var packet = new ZoneWar297MonsterCountResponse { MonsterNum = [.. quotas] };
        foreach (var player in zone.Players)
            if (!player.IsMovingZone)
                player.Session.Send(packet);
    }

    private void LogMonsterSummonGap(Zone zone, string what)
    {
        logger.LogWarning(
            "Valley of the Deceased zone {MapId}: legacy would summon {What} here -- skipped, documented gap, no client-facing monster appears",
            zone.MapId, what);
    }

    private void GrantRewardsToTribe(byte winningTribe)
    {
        foreach (var zone in zoneRegistry.Value.Zones)
        foreach (var player in zone.Players)
        {
            if (player.Tribe != winningTribe || player.IsMovingZone || player.IsDead)
                continue;

            zone.Post(ZoneCommand.GrantValleyWarRewardDrop(player.CharacterId));
        }
    }

    /// <summary>Contract §10: every session on THIS zone, unconditionally, no tribe/participation/transfer filter.</summary>
    private static void DisconnectEveryone(Zone zone)
    {
        // Deferred to after the full player scan, same posture as HoisundoCountdownSystem/DeathGateTickSystem's
        // own toForceQuit lists: Abort() only requests teardown, so disconnecting mid-enumeration here would
        // risk mutating the very collection this loop is iterating.
        List<PlayerRuntimeState>? toDisconnect = null;
        foreach (var player in zone.Players)
            (toDisconnect ??= []).Add(player);

        if (toDisconnect is null)
            return;

        foreach (var player in toDisconnect)
            if (player.Session is ClientSession client)
                client.Abort(DisconnectReason.ValleyWarForcedReset);
    }
}
