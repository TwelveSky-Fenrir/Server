namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class Zone051Zone053StateMap
{
    public static bool TryMapZone051(int selector, out int state)
    {
        state = selector switch
        {
            11 => 1,
            12 => 2,
            13 => 3,
            14 => 5,
            15 => 5,
            16 => 4,
            17 => 5,
            18 => 0,
            _ => -1
        };

        return state >= 0;
    }

    public static bool TryMapZone053(int selector, out int state)
    {
        state = selector switch
        {
            20 => 1,
            21 => 2,
            22 => 3,
            23 => 5,
            24 => 5,
            28 => 4,
            29 => 5,
            30 => 0,
            _ => -1
        };

        return state >= 0;
    }
}
