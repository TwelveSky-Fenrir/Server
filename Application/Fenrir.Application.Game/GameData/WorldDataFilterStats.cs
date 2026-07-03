namespace Fenrir.Application.Game.GameData;

/// <summary>
///     How many legacy orphan rows <see cref="WorldDataCacheBuilder.BuildZones" /> discarded while building the
///     zone index (rapport 05, risques: ~49% of world.MonsterSpawnRegions have a NULL ZoneNumber, ~54% of
///     world.ZonePortals have no destination -- both are dead data from zones/features this build never
///     shipped, filtered explicitly and logged rather than silently dropped inside a join).
/// </summary>
public sealed record WorldDataFilterStats(
    int PortalsWithoutDestination,
    int NpcPlacementsWithoutNpc,
    int SpawnRegionsWithoutZone,
    int SpawnRegionsWithoutMonster)
{
    public int TotalDiscarded =>
        PortalsWithoutDestination + NpcPlacementsWithoutNpc + SpawnRegionsWithoutZone + SpawnRegionsWithoutMonster;
}
