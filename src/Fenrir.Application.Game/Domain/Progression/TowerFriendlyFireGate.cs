namespace Fenrir.Application.Game.Domain.Progression;

public static class TowerFriendlyFireGate
{
    public static bool CanAttackGuardian(byte attackerTribe, byte? owningTribe, bool towerActivelyBuilt)
    {
        if (!towerActivelyBuilt)
            return false;

        return owningTribe is not { } owner || attackerTribe != owner;
    }
}
