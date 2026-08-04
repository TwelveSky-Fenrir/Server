using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Domain.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class Zone38BossScheduleEffects(
    WorldDataCache worldData,
    ILogger<Zone38BossScheduleEffects> logger,
    Lazy<IWorldNoticeService>? worldNotice = null) : IZone38BossScheduleEffects
{
    private const int BossServerIndex = 1_007_000;

    public bool TrySpawn(Zone zone, in Zone38BossSpawnRequest request, out Zone38BossInstance boss)
    {
        ArgumentNullException.ThrowIfNull(zone);

        boss = default;
        if (zone.MapId != Zone38BossSchedule.ZoneId || request.MonsterId != Zone38BossSchedule.BossMonsterId)
            return false;

        if (zone.TryGetMonster(BossServerIndex, out _))
        {
            logger.LogWarning(
                "Zone38 scheduled boss {MonsterId} was due but server index {ServerIndex} is still occupied",
                request.MonsterId, BossServerIndex);
            return false;
        }

        if (!worldData.MonstersById.TryGetValue(request.MonsterId, out var definition))
        {
            logger.LogError(
                "Zone38 scheduled boss {MonsterId} cannot spawn because its monster definition is unavailable",
                request.MonsterId);
            return false;
        }

        var entity = MonsterEntity.Create(BossServerIndex, zone.NextMonsterUniqueNumber(), definition.Monster,
            BossServerIndex, request.X, request.Y, request.Z);

        zone.SpawnMonster(entity);
        boss = new Zone38BossInstance(entity.ServerIndex, entity.UniqueNumber);
        return true;
    }

    public bool IsValid(Zone zone, in Zone38BossInstance boss)
    {
        ArgumentNullException.ThrowIfNull(zone);

        return zone.MapId == Zone38BossSchedule.ZoneId &&
               boss.ServerIndex == BossServerIndex &&
               zone.TryGetMonster(boss.ServerIndex, out var monster) &&
               monster is not null &&
               monster.UniqueNumber == boss.UniqueNumber &&
               monster.Template.MonsterId == Zone38BossSchedule.BossMonsterId;
    }

    public void AnnounceSpawned(Zone zone, in Zone38BossInstance boss)
    {
        ArgumentNullException.ThrowIfNull(zone);

        worldNotice?.Value.Broadcast("(Yanggok Valley) - Elite Boss has spawned");
        logger.LogInformation(
            "Zone38 scheduled boss {MonsterId} spawned at server index {ServerIndex}, unique number {UniqueNumber}",
            Zone38BossSchedule.BossMonsterId, boss.ServerIndex, boss.UniqueNumber);
    }

    public void AnnounceFinished(Zone zone, in Zone38BossInstance boss)
    {
        ArgumentNullException.ThrowIfNull(zone);

        worldNotice?.Value.Broadcast("(Yanggok Valley) - Elite Boss has Ended");
        logger.LogInformation(
            "Zone38 scheduled boss {MonsterId} finished at server index {ServerIndex}, unique number {UniqueNumber}",
            Zone38BossSchedule.BossMonsterId, boss.ServerIndex, boss.UniqueNumber);
    }
}
