using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.World;

/// <summary>game.usp_MonsterBossRespawnTimer_GetAll's only result set -- one row per pending boss respawn deadline.</summary>
[GenerateDto]
public sealed partial record MonsterBossRespawnTimerRowDto(
    int MonsterSpawnRegionId,
    DateTime NextSpawnUtc);
