using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class ValleyWarSystem(
    ValleyWarKillRegistry killRegistry,
    Lazy<ZoneEventBroadcaster> broadcaster,
    ILogger<ValleyWarSystem> logger) : ISimulationSystem
{
    private const int DoorContextTag = 0;

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

        return new ValleyWarEnvironmentSnapshot(eligiblePresent, zone.ValleyWarBossSlotOccupied());
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
            if (schedule.WinningTribe is { } winningTribe)
                GrantRewardsToTribe(zone, winningTribe);

            broadcaster.Value.AnnounceValleyWarBossDefeated();
        }

        if (result.PostWinReturnToTown)
            broadcaster.Value.AnnounceValleyWarReturnToTown();

        if (result.AllSessionsShouldDisconnect)
            DisconnectEveryone(zone);
    }

    private static void SendZoneLocalCountdown(Zone zone, int contextTag, int value)
    {
        var packet = new ZoneWar297StatusResponse { Value00 = contextTag, Value01 = contextTag, Value02 = value };
        foreach (var player in zone.Players)
            if (!player.IsMovingZone)
                player.Session.Send(packet);
    }

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

    private void GrantRewardsToTribe(Zone zone, byte winningTribe)
    {
        foreach (var player in zone.Players)
        {
            if (player.Tribe != winningTribe || player.IsMovingZone || player.IsDead)
                continue;

            if (!zone.Post(ZoneCommand.GrantValleyWarRewardDrop(player.CharacterId)))
                logger.LogWarning(
                    "Zone {MapId} inbox full: dropped valley-war reward drop grant for character {CharacterId}",
                    zone.MapId, player.CharacterId);
        }
    }

    private static void DisconnectEveryone(Zone zone)
    {
        List<PlayerRuntimeState>? toDisconnect = null;
        foreach (var player in zone.Players)
            (toDisconnect ??= []).Add(player);

        if (toDisconnect is null)
            return;

        foreach (var player in toDisconnect)
            if (player.Session is { } client)
                client.Abort(DisconnectReason.ValleyWarForcedReset);
    }
}
