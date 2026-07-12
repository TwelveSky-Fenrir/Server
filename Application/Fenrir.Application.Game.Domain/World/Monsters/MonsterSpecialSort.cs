namespace Fenrir.Application.Game.Domain.World.Monsters;

public static class MonsterSpecialSort
{
    public const byte Standard = 1;

    public const byte TribeSymbolStone = 2;

    public const byte Inert = 3;

    public const byte AllianceStone = 4;

    public const byte TribeGuard = 5;

    public const byte CarThrower = 6;

    public const byte Tower = 10;

    public static byte Derive(byte type, byte specialType)
    {
        if (type is 6 or 7 or 8 or 9)
            return TribeGuard;

        if (type != 1)
            return Standard;

        return specialType switch
        {
            10 => Tower,
            11 or 12 or 13 or 28 or 14 or 15 => TribeSymbolStone,
            21 or 22 or 23 or 29 => Inert,
            31 or 32 or 33 or 34 => AllianceStone,
            18 or 35 or 36 or 37 or 38 => CarThrower,
            _ => Standard
        };
    }
}
