namespace Fenrir.Application.Game.GameData;

public sealed record WorldDataFilterStats(
    int PortalsWithoutDestination,
    int NpcPlacementsWithoutNpc,
    int SpawnRegionsWithoutZone,
    int SpawnRegionsWithoutMonster)
{
    public int TotalDiscarded =>
        PortalsWithoutDestination + NpcPlacementsWithoutNpc +
        SpawnRegionsWithoutZone + SpawnRegionsWithoutMonster;
}
