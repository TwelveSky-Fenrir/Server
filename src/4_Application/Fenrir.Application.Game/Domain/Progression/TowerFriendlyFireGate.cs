namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     RvR guardian-tower friendly-fire gate.
///     Legacy parity: the per-tribe block inside <c>CanAttackTower</c> is commented out, so the tower-state
///     check ignores the attacker's tribe entirely and only gates on the tower being in a built (even) siege
///     state. The caller-side friendly-fire gate blocks solely the owning tribe (<c>attacker == towerTribe</c>);
///     its second condition (<c>ReturnAllianceTribe(attacker) == attacker</c>) is a dead bug that never fires
///     (<c>ReturnAlliance</c> never returns its own argument), so an ally of the owning tribe IS allowed to
///     damage the tower. An unresolved owner (-1) never occurs on a real tower server and blocks no one.
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:13575-13615 (CanAttackTower, per-tribe block commented,
///     even-state-only body), Server/ts25zone/S07_MyGame02.cpp:2119-2144 (caller gate, owner block +
///     inert ally condition), Server/Header/function.h:2964-2975 (ReturnAlliance never returns its argument).
/// </summary>
public static class TowerFriendlyFireGate
{
    public static bool CanAttackGuardian(byte attackerTribe, byte? owningTribe, bool towerActivelyBuilt)
    {
        if (!towerActivelyBuilt)
            return false;

        // Owner tribe cannot strike its own built tower. An ally of the owner IS allowed (the legacy
        // ally-block condition is inert), and an unresolved owner blocks no one.
        return owningTribe is not { } owner || attackerTribe != owner;
    }
}
