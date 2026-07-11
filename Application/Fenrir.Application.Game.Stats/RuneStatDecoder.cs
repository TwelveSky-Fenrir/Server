namespace Fenrir.Application.Game.Stats;

public static class RuneStatDecoder
{

        public const int SocketCount = 4;

        public static int Strength(int packed)
    {
        return (sbyte)packed;
    }

        public static int Dexterity(int packed)
    {
        return (sbyte)(packed >> 8);
    }

        public static int Vitality(int packed)
    {
        return (sbyte)(packed >> 16);
    }

        public static int Ki(int packed)
    {
        return (sbyte)(packed >> 24);
    }
}
