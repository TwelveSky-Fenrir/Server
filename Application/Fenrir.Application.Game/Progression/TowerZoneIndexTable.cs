namespace Fenrir.Application.Game.Progression;

/// <summary>Port of CHUGSOUNG_WAR_UI_ZoneIndex (S04_MyWork05.cpp:4946) -- zone number to one of the 12 tower slots.</summary>
public static class TowerZoneIndexTable
{
    /// <summary>-1 if <paramref name="zoneNumber" /> has no tower.</summary>
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
}
