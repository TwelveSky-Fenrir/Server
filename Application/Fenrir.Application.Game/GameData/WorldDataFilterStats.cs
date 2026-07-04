namespace Fenrir.Application.Game.GameData;

/// <summary>Legacy orphan rows discarded while building the zone index (~49% of MonsterSpawnRegions have a NULL ZoneNumber, ~54% of ZonePortals have no destination) -- logged, not silently dropped.</summary>
public sealed record WorldDataFilterStats(
    int PortalsWithoutDestination,
    int NpcPlacementsWithoutNpc,
    int SpawnRegionsWithoutZone,
    int SpawnRegionsWithoutMonster)
{
    public int TotalDiscarded =>
        PortalsWithoutDestination + NpcPlacementsWithoutNpc + SpawnRegionsWithoutZone + SpawnRegionsWithoutMonster;
}
