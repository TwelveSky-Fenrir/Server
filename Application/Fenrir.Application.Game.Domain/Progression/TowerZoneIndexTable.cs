namespace Fenrir.Application.Game.Domain.Progression;

public static class TowerZoneIndexTable
{

        public static int GetTowerIndex(int zoneNumber)
    {
        return zoneNumber switch
        {
            2 => 0,
            3 => 1,
            4 => 2,
            7 => 3,
            8 => 4,
            9 => 5,
            12 => 6,
            13 => 7,
            14 => 8,
            141 => 9,
            142 => 10,
            143 => 11,
            _ => -1
        };
    }

        public static byte? GetOwningTribe(int zoneNumber)
    {
        var towerIndex = GetTowerIndex(zoneNumber);
        return towerIndex < 0 ? null : (byte)(towerIndex / 3);
    }
}
