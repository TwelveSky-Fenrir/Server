using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

public sealed class RegularWarRewardGrantSink(ZoneRegistry zones, ILogger<RegularWarRewardGrantSink> logger)
    : IRegularWarEventSink
{
    public void OnCountdownAnnounced(short mapId, int remainingMinutes)
    {
    }

    public void OnCountdownFinished(short mapId)
    {
    }

    public void OnGateOpened(short mapId)
    {
    }

    public void OnSmallestTribeFlagged(short mapId, byte tribeId)
    {
        if (!zones.TryGet(mapId, out var zone))
        {
            logger.LogWarning(
                "RegularWar {MapId}: smallest-tribe flag ({TribeId}) computed but this shard no longer hosts the map -- dropped",
                mapId, tribeId);
            return;
        }

        if (!zone.Post(ZoneCommand.SetRegularWarSmallestTribe(tribeId)))
            logger.LogWarning(
                "RegularWar {MapId}: smallest-tribe flag ({TribeId}) dropped -- zone inbox full", mapId, tribeId);
    }

    public void OnActiveWarStarted(short mapId, int durationLegacyTicks)
    {
    }

    public void OnWarConcluded(short mapId, RegularWarOutcome outcome, byte? winningTribe,
        ImmutableArray<RegularWarRewardGrant> rewards, bool bossMonstersShouldSpawn)
    {
        if (!rewards.IsDefaultOrEmpty)
        {
            if (!zones.TryGet(mapId, out var rewardZone))
                logger.LogWarning(
                    "RegularWar {MapId}: {Count} reward grant(s) computed but this shard no longer hosts the map -- dropped",
                    mapId, rewards.Length);
            else
                foreach (var grant in rewards)
                    if (!rewardZone.Post(ZoneCommand.ApplyRegularWarReward(grant)))
                        logger.LogWarning(
                            "RegularWar {MapId}: reward grant for character {CharacterId} dropped -- zone inbox full",
                            mapId, grant.CharacterId);
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

    public void OnReturnToTownAnnounced(short mapId)
    {
    }

    public void OnMonstersShouldDespawn(short mapId)
    {
        if (!zones.TryGet(mapId, out var zone))
        {
            logger.LogWarning(
                "RegularWar {MapId}: boss cleanup due but this shard no longer hosts the map -- not applied",
                mapId);
            return;
        }

        var completion =
            new TaskCompletionSource<ZoneCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!zone.Post(ZoneCommand.DespawnRegularWarBosses(completion)))
        {
            logger.LogWarning("RegularWar {MapId}: boss cleanup was backpressured", mapId);
            return;
        }

        _ = ObserveBossCleanupAsync(mapId, completion.Task);
    }

    public void OnAllSessionsShouldDisconnect(short mapId)
    {
        if (!zones.TryGet(mapId, out var zone))
        {
            logger.LogWarning(
                "RegularWar {MapId}: forced-reset disconnect due but this shard no longer hosts the map -- dropped",
                mapId);
            return;
        }

        var players = zone.Players.ToList();
        players.Sort(static (left, right) => left.CharacterId.CompareTo(right.CharacterId));

        foreach (var player in players)
            player.Session.Abort(DisconnectReason.Evicted);

        logger.LogInformation("RegularWar {MapId}: forced-reset aborted {SessionCount} hosted player session(s)",
            mapId, players.Count);
    }

    private async Task ObserveBossCleanupAsync(short mapId, Task<ZoneCommandResult> completion)
    {
        try
        {
            var result = await completion.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            if (result.Kind != ZoneCommandResultKind.Applied)
                logger.LogWarning("RegularWar {MapId}: boss cleanup was not applied ({Result}, {Cause})", mapId,
                    result.Kind, result.Cause);
        }
        catch (TimeoutException)
        {
            logger.LogWarning("RegularWar {MapId}: boss cleanup actor acknowledgement timed out", mapId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RegularWar {MapId}: boss cleanup acknowledgement faulted", mapId);
        }
    }
}
