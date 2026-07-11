namespace Fenrir.Application.Game.Domain.World.Monsters;

public static class DungeonSpawnDensityPolicy
{
    public const int MinConfiguredCountForBump = 5;

    public const int MaxConfiguredCountForBump = 19;

    public const int BumpedSpawnCount = 20;

    public const int MonsterIdBumpCeilingExclusive = 500;

    private const int CapacityMultiplierWhenDungeon = 2;

    public static int ResolveConfiguredSpawnCount(bool isDungeonZone, int configuredCount, int resolvedMonsterId)
    {
        if (!isDungeonZone)
            return configuredCount;

        if (configuredCount is < MinConfiguredCountForBump or > MaxConfiguredCountForBump)
            return configuredCount;

        if (resolvedMonsterId >= MonsterIdBumpCeilingExclusive)
            return configuredCount;

        return BumpedSpawnCount;
    }

    public static int ResolveTableCapacity(bool isDungeonZone, int baseCapacity)
    {
        return isDungeonZone ? baseCapacity * CapacityMultiplierWhenDungeon : baseCapacity;
    }
}
