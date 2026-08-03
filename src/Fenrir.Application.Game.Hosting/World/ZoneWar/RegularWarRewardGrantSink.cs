using System.Collections.Immutable;
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

    public void OnActiveWarStarted(short mapId)
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

    public void OnMonstersShouldDespawn(short mapId)
    {
    }

    public void OnAllSessionsShouldDisconnect(short mapId)
    {
    }
}
