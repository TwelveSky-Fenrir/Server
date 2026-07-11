namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class HolyStoneTribeMatch
{

        public static bool Matches(byte tribe, byte? holderTribe, byte? allyOfHolderTribe)
    {
        if (holderTribe is not { } holder)
            return false;

        return tribe == holder || tribe == allyOfHolderTribe;
    }
}
