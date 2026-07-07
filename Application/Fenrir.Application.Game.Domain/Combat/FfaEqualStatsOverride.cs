using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     FFA zone (335) "equal real stats" override for attack resolution -- <c>MyFactor</c>'s
///     <c>USE_FFA_EQUAL_REAL_STATS</c> block (<c>Server/Header/Protocol/MyFactor.cpp:58-96</c>, bare
///     file-scope <c>#define</c>, not gated by any build variant). Every one of the nine stat getters
///     <c>AttackPlayer</c>/<c>ProcessAttack03/04</c> read to resolve an attack -- attack power, defense
///     power, attack-success ("hit"), attack-block (actually the dodge constant, see remarks), elemental
///     attack power, elemental defense power, critical rate, critical-defense, and luck -- opens with
///     <c>if (FFA_CheckEqualStatsZone(mZoneNumber)) return FFA_STAT_X();</c> (<c>:4009-4478</c>), a pure,
///     unconditional substitution keyed only on the combatant's own current zone number equalling
///     <see cref="PvpKillRewardZoneCatalog.FfaMapNumber" /> (335). Real equipment/level/buff/pet/socket math
///     is not consulted at all when the override is active; every combatant in the zone gets the identical
///     fixed value regardless of gear or level.
/// </summary>
/// <remarks>
///     <para>
///         The legacy function named for "attack block" actually returns the same fixed constant used
///         elsewhere in the same source as the dodge constant (<c>MyFactor.cpp:4217-4222</c> vs. <c>:80</c>)
///         -- the function name and the stat it represents do not literally match in the legacy source
///         itself; this is preserved here as-is (<see cref="EffectiveStats.AttackBlock" /> still receives
///         the value, matching every other override site in this codebase that reads that field as the
///         dodge-chance input to <c>CombatMath.ComputeHitChancePercent</c>).
///     </para>
///     <para>
///         Deliberately NOT covered by this override: <see cref="EffectiveStats.MaxLife" /> and
///         <see cref="EffectiveStats.MaxMana" />. The behavior contract this was built from flags a second,
///         out-of-scope occurrence of the same zone check inside "base" (pre-equipment) stat getters and
///         four base-attribute getters (including base max-life/max-mana) as independently observed but
///         NOT part of the nine getters this contract actually covers -- so no HP/MP override is applied
///         here. If a future contract confirms a "base" stat read reaches combat resolution independently
///         of the nine final reads covered here, that is a separate follow-up, not something to guess at
///         by extending this method.
///     </para>
/// </remarks>
public static class FfaEqualStatsOverride
{
    // Server/Header/Protocol/MyFactor.cpp:75-94 (FFA_STAT_X() constants).
    private const int FixedAttackPower = 45000;
    private const int FixedDefensePower = 30000;
    private const int FixedAttackSuccess = 30000;
    private const int FixedAttackBlock = 5000;
    private const int FixedElementAttackPower = 1000;
    private const int FixedElementDefensePower = 500;
    private const int FixedCritical = 100;
    private const int FixedCriticalDefence = 65;
    private const int FixedLuck = 300;

    /// <summary>
    ///     True when <paramref name="zoneId" /> is the dedicated FFA arena (335) where
    ///     <see cref="Apply" />'s equal-real-stats override is unconditionally active for every combatant
    ///     present -- <c>FFA_CheckEqualStatsZone</c> (<c>MyFactor.cpp:66-69</c>), a pure zone-number equality
    ///     test with no other precondition (not gated by tribe, level, gear, buffs, or session state).
    /// </summary>
    public static bool IsEqualStatsZone(short zoneId)
    {
        return zoneId == PvpKillRewardZoneCatalog.FfaMapNumber;
    }

    /// <summary>
    ///     Returns <paramref name="realStats" /> unchanged outside zone 335. Inside it, returns
    ///     <paramref name="realStats" /> with all nine combat-relevant stats replaced by the fixed FFA
    ///     constants above, preserving <see cref="EffectiveStats.MaxLife" />/<see cref="EffectiveStats.MaxMana" />
    ///     from the real, computed value (see class remarks for why those two are out of this override's
    ///     scope). Call this once per combatant snapshot built for attack resolution -- it is a pure,
    ///     side-effect-free substitution, not a mutation of any cached/persisted stat.
    /// </summary>
    public static EffectiveStats Apply(short zoneId, EffectiveStats realStats)
    {
        if (!IsEqualStatsZone(zoneId))
            return realStats;

        return realStats with
        {
            AttackPower = FixedAttackPower,
            DefensePower = FixedDefensePower,
            AttackSuccess = FixedAttackSuccess,
            AttackBlock = FixedAttackBlock,
            ElementAttackPower = FixedElementAttackPower,
            ElementDefensePower = FixedElementDefensePower,
            Critical = FixedCritical,
            CriticalDefence = FixedCriticalDefence,
            Luck = FixedLuck
        };
    }
}
