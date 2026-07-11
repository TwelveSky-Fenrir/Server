namespace Fenrir.Application.Game.Domain.Progression;

public static class TowerGuardianCatalog
{

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
            1 => 589,
            2 => 593,
            _ => 597
        };
        return typeBase + levelIndex;
    }

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
