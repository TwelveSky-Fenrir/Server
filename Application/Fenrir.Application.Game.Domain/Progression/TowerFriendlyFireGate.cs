namespace Fenrir.Application.Game.Domain.Progression;

public static class TowerFriendlyFireGate
{

        public static bool CanAttackGuardian(byte attackerTribe, byte? owningTribe, bool towerActivelyBuilt,
        byte? allyOfOwningTribe)
    {
        if (owningTribe is not { } owner)
            return false;
        if (!towerActivelyBuilt)
            return false;
        if (attackerTribe == owner)
            return false;
        if (allyOfOwningTribe == attackerTribe)
            return false;

        return true;
    }
}
