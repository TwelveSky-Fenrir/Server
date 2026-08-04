using Fenrir.Application.Game.Domain.Simulation;
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
        if (!ValleyWarMapCatalog.IsCoordinator(zone.MapId) || legacyTicksElapsed <= 0)
            return;

        if (!broadcaster.Value.HasCompleteValleyWarCampaignOwnership())
            return;

        for (var i = 0; i < legacyTicksElapsed; i++)
        {
            var snapshot = broadcaster.Value.GetValleyWarEnvironmentSnapshot();
            if (!killRegistry.TryTick(zone.MapId, snapshot, out var schedule, out var result))
                return;

            React(zone, schedule, result);
        }
    }

    private void React(Zone zone, ValleyWarSchedule schedule, ValleyWarTickResult result)
    {
        if (result.GateCountdownValue is { } remaining)
            if (!RequirePublished(broadcaster.Value.AnnounceValleyWarGateCountdown(remaining)))
                return;

        if (result.GateOpened)
            if (!RequirePublished(broadcaster.Value.AnnounceValleyWarGateOpened()))
                return;

        if (result.GateClosed)
            if (!RequirePublished(broadcaster.Value.AnnounceValleyWarGateClosed()))
                return;

        if (result.DoorCountdownValue is { } doorCountdown)
            broadcaster.Value.AnnounceValleyWarCountdown(DoorContextTag, doorCountdown);

        if (result.DoorOpened)
        {
            if (!RequirePublished(broadcaster.Value.AnnounceValleyWarDoorOpened()))
                return;

            LogMonsterSummonGap(zone,
                "the general kill-race monster population (no id/count/coordinate data available)");
        }

        if (result.KillRaceQuotas is { } quotas)
            broadcaster.Value.AnnounceValleyWarKillRaceQuotas(quotas);

        if (result.KillRaceCountdownValue is { } killRaceCountdown)
            broadcaster.Value.AnnounceValleyWarCountdown(RaceOrBossContextTag, killRaceCountdown);

        if (result.KillRaceEndedEmptyOrTimeout)
            if (!RequirePublished(broadcaster.Value.AnnounceValleyWarReturnToTown()))
                return;

        if (result.TribeWin && result.WinningTribe is { } winner)
        {
            if (!RequirePublished(broadcaster.Value.AnnounceValleyWarTribeWin(winner)))
                return;

            zone.HandleSummonValleyWarBoss();
        }

        if (result.BattleScrollDeleted)
            if (!RequirePublished(broadcaster.Value.AnnounceValleyWarBattleScrollDeleted()))
                return;

        if (result.BossWindowCountdownValue is { } bossCountdown)
            broadcaster.Value.AnnounceValleyWarCountdown(RaceOrBossContextTag, bossCountdown);

        if (result.BossWindowTimeout)
        {
            if (!RequirePublished(broadcaster.Value.AnnounceValleyWarReturnToTown()))
                return;

            zone.DespawnValleyWarBossPool();
        }

        if (result.BossWin)
        {
            if (!RequirePublished(broadcaster.Value.AnnounceValleyWarBossDefeated()))
                return;

            if (schedule.WinningTribe is { } winningTribe)
                broadcaster.Value.GrantValleyWarRewardsToTribe(winningTribe);
        }

        if (result.PostWinReturnToTown)
        {
            if (!RequirePublished(broadcaster.Value.AnnounceValleyWarReturnToTown()))
                return;

            zone.DespawnValleyWarBossPool();
        }

        if (result.AllSessionsShouldDisconnect)
            broadcaster.Value.DisconnectValleyWarCampaign();
    }

    private void LogMonsterSummonGap(Zone zone, string what)
    {
        logger.LogWarning(
            "Valley of the Deceased zone {MapId}: legacy would summon {What} here -- skipped, documented gap, no client-facing monster appears",
            zone.MapId, what);
    }

    private bool RequirePublished(bool published)
    {
        if (published)
            return true;

        killRegistry.MarkUnavailable();
        return false;
    }
}
