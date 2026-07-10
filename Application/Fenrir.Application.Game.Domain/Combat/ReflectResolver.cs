namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>Result of the pre-main cross-avatar destroyer + reflect step (<see cref="ReflectResolver.Resolve" />).</summary>
/// <param name="DestroyerSucceeded">
///     The attacker's Holy-Shield-destroyer roll landed -- the caller must hard-clear the defender's Holy-Shields
///     (<see cref="HolyShieldResolver.RemoveAll" />). Independent of <paramref name="ReflectFired" />: the
///     destroyer roll changes no damage value and never aborts the attack.
/// </param>
/// <param name="ReflectFired">
///     The defender's reflect fired -- the caller applies <paramref name="ReflectDamage" /> to the ATTACKER and
///     the defender takes no damage this hit (the whole attack aborts).
/// </param>
/// <param name="ReflectDamage">150% of the finalized pre-shield/pre-element main damage; 0 unless reflect fired.</param>
public readonly record struct ReflectOutcome(bool DestroyerSucceeded, bool ReflectFired, int ReflectDamage);

/// <summary>
///     The cross-avatar "destroyer roll + reflect" step that runs after the raw main damage is finalized but
///     BEFORE Holy-Shield absorption, element addition, and the life subtraction
///     (<c>Server/ts25zone/S07_MyGame02.cpp:1267-1334</c>). Pure -- draws its rolls from the supplied
///     <see cref="IRandomSource" /> in legacy order (destroyer first, then the two reflect rolls) and returns a
///     verdict; the caller owns every state change (shield clear, reflect damage, kill/credit).
/// </summary>
/// <remarks>
///     <para>
///         Ships the LIVE tuning bases, not the commented legacy ones: the destroyer buff is active over
///         [1, 201] (commented 101) and rolls on a 1000 base; the reflect buff is active over [1, 150]
///         (commented 50) and gates on a 1500-base probability roll AND a 5-base roll landing exactly on zero
///         (commented bases 1000 and 3) -- so reflect fires at roughly one-in-five times probability-over-1500.
///     </para>
///     <para>
///         The reflect probability starts at the defender's reflect-buff strength, is reduced by three per level
///         the attacker out-levels the defender, then by the attacker's anti-reflect stat, then clamped to at
///         least zero. TODO(world-state): no anti-reflect stat is modelled on
///         <see cref="Stats.EffectiveStats" /> yet, so <c>ResolvePvmAttack</c>'s callers pass 0 -- reflect is
///         therefore currently never reduced by that term. Wire a real value here once the stat exists; nothing
///         else about this resolver changes.
///     </para>
/// </remarks>
public static class ReflectResolver
{
    /// <summary>Attacker BUFF_INFO slot 14 -- <c>DestroySuccessUp</c> (skill 105, <see cref="Skills.SkillEffectCatalog" />).</summary>
    public const int DestroyerBuffSlot = 14;

    /// <summary>Defender BUFF_INFO slot 12 -- <c>ReturnSuccessUp</c> (skill 103, <see cref="Skills.SkillEffectCatalog" />).</summary>
    public const int ReflectBuffSlot = 12;

    private const int DestroyerActiveMin = 1;
    private const int DestroyerActiveMax = 201;
    private const int DestroyerRollBase = 1000;

    private const int ReflectActiveMin = 1;
    private const int ReflectActiveMax = 150;
    private const int ReflectProbabilityRollBase = 1500;
    private const int ReflectGateRollBase = 5;
    private const int ReflectLevelPenaltyPerLevel = 3;

    public static ReflectOutcome Resolve(int attackerDestroyerBuff, int defenderReflectBuff, int attackerLevel,
        int defenderLevel, int attackerAntiReflectStat, bool reflectEnabled, int preElementMainDamage,
        IRandomSource rng)
    {
        var destroyerSucceeded = false;
        if (attackerDestroyerBuff is >= DestroyerActiveMin and <= DestroyerActiveMax)
            destroyerSucceeded = rng.NextInt32(DestroyerRollBase) < attackerDestroyerBuff;

        var reflectFired = false;
        if (reflectEnabled && defenderReflectBuff is >= ReflectActiveMin and <= ReflectActiveMax)
        {
            var probability = defenderReflectBuff;
            if (attackerLevel > defenderLevel)
                probability -= ReflectLevelPenaltyPerLevel * (attackerLevel - defenderLevel);
            probability -= attackerAntiReflectStat;
            if (probability < 0)
                probability = 0;

            // Both rolls are always drawn (no short-circuit) -- "two independent rolls are drawn."
            var probabilityRoll = rng.NextInt32(ReflectProbabilityRollBase);
            var gateRoll = rng.NextInt32(ReflectGateRollBase);
            reflectFired = gateRoll == 0 && probabilityRoll < probability;
        }

        // 150% of the pre-element main damage, exact legacy int truncation (x * 3 / 2), no float.
        var reflectDamage = reflectFired ? preElementMainDamage * 3 / 2 : 0;
        return new ReflectOutcome(destroyerSucceeded, reflectFired, reflectDamage);
    }
}
