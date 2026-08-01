using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

[GenerateDto]
public sealed partial record MonsterBossRespawnTimerRowDto(
    int MonsterSpawnRegionId,
    DateTime NextSpawnUtc);
