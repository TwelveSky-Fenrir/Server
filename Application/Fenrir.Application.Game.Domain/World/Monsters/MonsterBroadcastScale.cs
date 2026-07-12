namespace Fenrir.Application.Game.Domain.World.Monsters;

public static class MonsterBroadcastScale
{
    public const int MaxScale = 3;

    public static int ForMonster(byte monsterType, byte monsterSpecialType)
    {
        if (monsterType == 1)
            return monsterSpecialType switch
            {
                11 or 12 or 13 or 14 or 15 or 28
                    or 18 or 21 or 22 or 23 or 29 or 31 or 32 or 33 or 34 or 35 or 36 or 37 or 38 => 3,
                _ => 1
            };

        return monsterType is 6 or 7 or 8 or 9 ? 2 : 1;
    }
}
