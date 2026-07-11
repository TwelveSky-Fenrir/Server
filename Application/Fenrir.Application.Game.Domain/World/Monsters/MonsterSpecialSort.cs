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
            11 or 12 or 13 or 28 or 14 or 15 => TribeSymbolStone,
            _ => Standard
        };
    }
}
