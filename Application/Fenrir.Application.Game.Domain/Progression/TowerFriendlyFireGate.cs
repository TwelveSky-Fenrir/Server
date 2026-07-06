namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     Avatar -&gt; tower-guardian attack authorization -- the <c>mSpecialSortNumber</c> == 10 sub-branch of
///     <c>ProcessAttack03</c>'s "Avatar -&gt; Monster" dispatch. Implements the INTENDED rule, not the legacy
///     bug it replaces -- see remarks.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame02.cpp:1825 (function entry), :1938 (outer
///     <c>mSpecialSortNumber</c> dispatch), :2107-2158 (tower sub-branch, <c>USE_TOWER</c> --
///     Server/Header/Protocol/DEFINE.h:86 defines it unconditionally, so this is live in every build) ;
///     Server/ts25zone/S07_MyGame01.cpp:13575-13615 (<c>CanAttackTower</c> -- the per-tribe tower-group total
///     check at :13577-13604 is fully commented out/dead; the only live logic, :13606-13614, tests whether
///     this zone's own tower is built to an active level and never reads its own <c>tTribe</c> parameter) ;
///     Server/ts25zone/S07_MyGame01.cpp:1339-1356 (<c>bIsTowerTypeServer</c>/owning-tribe table, mirrored by
///     <see cref="TowerZoneIndexTable.GetOwningTribe" />) ; Server/Header/function.h:2964-2975
///     (<c>ReturnAlliance</c> -- looking up the ally of a tribe always returns the *other* member of whichever
///     pair it belongs to, never the tribe passed in) ; Server/ts25zone/S07_MyGame01.cpp:3092-3107
///     (<c>MyGame::ReturnAllianceTribe</c> wrapper -- its zone-specific override branches are entirely
///     commented out, so it delegates straight through) ; Server/ts25zone/S07_MyGame02.cpp:1461-1501, :2008,
///     :2033 (<c>CanAttackStoneType3..6</c> -- the correctly-implemented "block the owner tribe and the
///     owner's ally" analog for the game's other faction-owned objectives, called with an ally value derived
///     from the *owner* tribe -- the contrast that shows what the tower check should have compared).
///     <para>
///         PRESERVED-BUG-NOT-PORTED: the legacy resolves the ally of the ATTACKER's own tribe and compares
///         that value back against the attacker (S07_MyGame02.cpp:2142-2143). Because
///         <c>ReturnAlliance</c>/<c>ReturnAllianceTribe</c> never return the tribe passed to them, that
///         comparison can never be true for any real alliance configuration, so the ally exemption never
///         actually fires in the legacy binary -- an attacker whose tribe is allied with the tower's owning
///         tribe is never blocked there. This method implements the INTENDED rule instead (ally of the
///         tower's OWNING tribe, matching the stone-objective analog above), per this behavior's own
///         translated contract. Do not "restore" the original attacker-ally comparison; it is the confirmed
///         bug this gate exists to fix.
///     </para>
/// </remarks>
public static class TowerFriendlyFireGate
{
    /// <param name="attackerTribe">The attacking player's own tribe (0-3).</param>
    /// <param name="owningTribe">
    ///     <see cref="TowerZoneIndexTable.GetOwningTribe" /> for the zone hosting this guardian; null when the
    ///     zone hosts no recognized tower slot at all -- the attack is rejected outright in that case, before
    ///     any tribe comparison runs (S07_MyGame02.cpp:2108-2110).
    /// </param>
    /// <param name="towerActivelyBuilt">
    ///     Whether this zone's tower is currently built to an active level (legacy <c>CanAttackTower</c>,
    ///     ignoring its own tribe parameter -- see class remarks). Under construction, destroyed, or
    ///     never-built all reject the attack.
    /// </param>
    /// <param name="allyOfOwningTribe">
    ///     The tribe currently allied with <paramref name="owningTribe" />, or null if it has no active
    ///     alliance. Must already be resolved against the OWNER's tribe, never the attacker's own -- passing
    ///     the ally of <paramref name="attackerTribe" /> here reproduces the legacy bug instead of fixing it.
    /// </param>
    /// <returns>True if the attack may proceed into the shared damage-resolution path.</returns>
    public static bool CanAttackGuardian(byte attackerTribe, byte? owningTribe, bool towerActivelyBuilt,
        byte? allyOfOwningTribe)
    {
        if (owningTribe is not { } owner)
            return false; // not a tower zone, or an unrecognized server number reached this branch anyway
        if (!towerActivelyBuilt)
            return false;
        if (attackerTribe == owner)
            return false; // self-tribe protection (S07_MyGame02.cpp:2143) -- correct in the legacy too
        if (allyOfOwningTribe == attackerTribe)
            return false; // the actual fix: block the owner's ally, not the attacker's own (impossible) ally

        return true;
    }
}
