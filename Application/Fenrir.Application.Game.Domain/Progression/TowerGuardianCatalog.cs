namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     Static facts about the 12 towers' guardian monster the legacy hardcoded rather than storing per-tower:
///     which world.Monsters row stands watch at a given level/type (<c>MySummon::SummonMonsterForTribeTower</c>,
///     S10_MySummon.cpp:2159-2225), and where it stands in its zone (<c>MyGame::mTowerLocation</c>,
///     S07_MyGame01.cpp:1341-1352). Both are pure structural formulas/tables, same footing as
///     <see cref="TowerZoneIndexTable" />'s own zone-number switch -- the monster's own stats (life, damage,
///     experience...) are fully database-driven via world.Monsters, not duplicated here.
/// </summary>
public static class TowerGuardianCatalog
{
    /// <summary>
    ///     world.Monsters IDs 589-600: 3 types (Silver/CP/EXP) x 4 levels, seeded in that exact 12-row block
    ///     (Database/70_seed/world/090_monsters.sql). Returns 0 (no such monster) for any out-of-range input --
    ///     callers should treat that as "stay in Building, retry next tick" rather than throw.
    /// </summary>
    public static int ResolveMonsterId(int level, int towerType)
    {
        var levelIndex = level switch
        {
            2 => 0,
            4 => 1,
            6 => 2,
            8 => 3,
            _ => -1
        };
        if (levelIndex < 0 || towerType is < 1 or > 3)
            return 0;

        var typeBase = towerType switch
        {
            1 => 589, // Silver Tower
            2 => 593, // CP Tower
            _ => 597 // EXP Tower
        };
        return typeBase + levelIndex;
    }

    /// <summary>
    ///     The guardian's fixed stand point for the tower hosted by <paramref name="zoneNumber" />, or false if
    ///     that zone hosts no tower.
    /// </summary>
    public static bool TryGetGuardianLocation(short zoneNumber, out float x, out float y, out float z)
    {
        (x, y, z) = zoneNumber switch
        {
            2 => (-1276f, -5f, 1826f),
            3 => (-8086f, 0f, 6225f),
            4 => (3770f, 95f, 3173f),
            7 => (-1879f, 2f, -1105f),
            8 => (7326f, 40f, 4224f),
            9 => (-3703f, -593f, 6223f),
            12 => (-1306f, -2f, -380f),
            13 => (-7897f, 9f, 1899f),
            14 => (6290f, 340f, 4775f),
            141 => (4289f, 0f, 3645f),
            142 => (32f, 0f, 2663f),
            143 => (-67f, -12f, 3046f),
            _ => (0f, 0f, 0f)
        };
        return TowerZoneIndexTable.GetTowerIndex(zoneNumber) >= 0;
    }
}
