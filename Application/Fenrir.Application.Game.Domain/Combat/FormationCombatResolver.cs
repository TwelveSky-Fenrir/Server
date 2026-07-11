namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Branch A of the B10 unimplemented-combat-branches contract: the world-scope tribe "Formation" ability
///     (legacy <c>mWorldInfo-&gt;mTribeMasterCallAbility[tribe]</c>, one code per tribe, pushed from the hub)
///     applied to an ENEMY-CLASS (cross-tribe / RvR) player-on-player hit inside <c>AttackPlayer</c>. A
///     duel-class hit never evaluates any Formation code
///     (<c>Server/ts25zone/S07_MyGame02.cpp:1068-1256</c>). Pure: the per-tribe code is resolved by the caller
///     from world state and passed in; this class never reads world/zone state itself.
/// </summary>
/// <remarks>
///     <para>
///         The four codes are mutually exclusive per tribe (a tribe holds at most one at a time); any stored
///         value other than 1/2/3/4 -- including the cleared value 0 -- is inert. The source of the value is the
///         hub (sub-command 302 sets, sub-command 45 clears --
///         <c>
///             Server/ts25center/S04_MyWork02.cpp:401-405,
///             921-925
///         </c>
///         ); the zone only reads it. Codes:
///         <list type="bullet">
///             <item>
///                 1 (<see cref="AttackerPowerBoostCode" />): the ATTACKER's tribe holding it scales the
///                 attacker's attack power up by one-tenth before the raw damage subtraction (:1071-1074).
///             </item>
///             <item>
///                 2 (<see cref="DefenderDefenseBoostCode" />): the DEFENDER's tribe holding it scales the
///                 defender's defense power up by one-tenth before the raw damage subtraction (:1076-1078).
///             </item>
///             <item>
///                 3 (<see cref="AttackerCriticalBoostCode" />): the ATTACKER's tribe holding it (and the
///                 defender NOT holding 4) raises the critical threshold by five points (:1174-1181).
///             </item>
///             <item>
///                 4 (<see cref="DefenderCriticalReductionCode" />): the DEFENDER's tribe holding it (and the
///                 attacker NOT holding 3) lowers the critical threshold by five points (:1183-1190). Both
///                 present at once cancel to the unadjusted threshold (:1192-1200).
///             </item>
///         </list>
///     </para>
///     <para>
///         "Scaled up by one-tenth" is implemented as the integer increase <c>value + value / 10</c> (identical
///         to <c>value * 11 / 10</c> for every non-negative value). The exact C++ expression at :1071-1078
///         (an integer form vs. a float 1.1 multiply) is not reproduced by the contract; the two integer forms
///         coincide and a float multiply would differ only at representation edges -- see the B10 open questions.
///     </para>
/// </remarks>
public static class FormationCombatResolver
{
    /// <summary>No Formation ability held (or any code outside 1-4) -- every branch below is inert.</summary>
    public const byte NoFormation = 0;

    /// <summary>Attacker's tribe: scale the attacker's attack power up by one-tenth (:1071-1074).</summary>
    public const byte AttackerPowerBoostCode = 1;

    /// <summary>Defender's tribe: scale the defender's defense power up by one-tenth (:1076-1078).</summary>
    public const byte DefenderDefenseBoostCode = 2;

    /// <summary>Attacker's tribe: raise the critical threshold by <see cref="CriticalThresholdShift" /> (:1174-1181).</summary>
    public const byte AttackerCriticalBoostCode = 3;

    /// <summary>Defender's tribe: lower the critical threshold by <see cref="CriticalThresholdShift" /> (:1183-1190).</summary>
    public const byte DefenderCriticalReductionCode = 4;

    /// <summary>Points the critical threshold moves per qualifying Formation code (:1174-1190).</summary>
    public const int CriticalThresholdShift = 5;

    /// <summary>
    ///     The attacker's attack power after Formation code 1, else unchanged. Apply immediately before the raw
    ///     attack-minus-defense subtraction (contract branch A.1). Enemy-class hits only -- the caller passes
    ///     <see cref="NoFormation" /> on a duel hit.
    /// </summary>
    public static int ScaleAttackPower(int attackPower, byte attackerFormationCode)
    {
        return attackerFormationCode == AttackerPowerBoostCode ? ScaleUpByTenth(attackPower) : attackPower;
    }

    /// <summary>
    ///     The defender's defense power after Formation code 2, else unchanged. Apply immediately before the raw
    ///     attack-minus-defense subtraction (contract branch A.2). Enemy-class hits only.
    /// </summary>
    public static int ScaleDefensePower(int defensePower, byte defenderFormationCode)
    {
        return defenderFormationCode == DefenderDefenseBoostCode ? ScaleUpByTenth(defensePower) : defensePower;
    }

    /// <summary>
    ///     The final critical-roll threshold: the base comparison (the attacker's critical minus the defender's
    ///     critical-defense, floored at zero -- <c>getCriticalAttackDefValue</c>, :554-568) adjusted by the
    ///     Formation critical codes (:1165-1200). The result may be non-positive when code 4 pushes it below
    ///     zero; the caller's own "&gt; 0" guard before the crit roll then treats it as "no critical", matching
    ///     legacy <c>rand()%100 &lt; threshold</c>. Enemy-class only -- pass <see cref="NoFormation" /> for both
    ///     codes on a duel hit to get the unadjusted (but still floored, hence roll-equivalent) value.
    /// </summary>
    public static int AdjustCriticalChance(int attackerCritical, int defenderCriticalDefence,
        byte attackerFormationCode, byte defenderFormationCode)
    {
        var baseChance = attackerCritical - defenderCriticalDefence;
        if (baseChance < 0)
            baseChance = 0;
        return baseChance + CriticalThresholdDelta(attackerFormationCode, defenderFormationCode);
    }

    /// <summary>
    ///     The signed critical-threshold adjustment from the two tribes' Formation codes (:1174-1200): +5 when
    ///     the attacker holds 3 and the defender does not hold 4; -5 when the defender holds 4 and the attacker
    ///     does not hold 3; 0 when both or neither hold their code.
    /// </summary>
    public static int CriticalThresholdDelta(byte attackerFormationCode, byte defenderFormationCode)
    {
        var attackerBoosts = attackerFormationCode == AttackerCriticalBoostCode;
        var defenderReduces = defenderFormationCode == DefenderCriticalReductionCode;
        if (attackerBoosts && !defenderReduces)
            return CriticalThresholdShift;
        if (defenderReduces && !attackerBoosts)
            return -CriticalThresholdShift;
        return 0;
    }

    private static int ScaleUpByTenth(int value)
    {
        return value + value / 10;
    }
}
