namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Pure pet-growth-experience-amount resolution for the PvP/RvR "kill other tribe" reward pipeline
///     (<c>MyUtil::ProcessForKillOtherTribe</c>'s own separate pet-exp branch, S07_MyGame03.cpp:3173-3188) --
///     the sibling of <see cref="PvpKillExperienceCalculator" />'s character-EXP formula, computed
///     independently and forwarded alongside it into the same shared crediting call
///     (<c>MyUtil::ProcessForExperience</c>, S07_MyGame03.cpp:318-321) that
///     <see cref="Fenrir.Application.Game.Domain.World.Zone.GrantMonsterKillExperience" />'s own pet-exp
///     branch (<see cref="Fenrir.Application.Game.Domain.Pets.PetExperienceCreditResolver" />) also routes
///     through for the PvE side. No I/O, no state -- <see cref="Fenrir.Application.Game.Domain.World.Zone" />
///     is the only caller and owns the attacker's combined-level input this needs.
/// </summary>
/// <remarks>
///     <para>
///         Unlike the character-EXP amount computed moments earlier in the same legacy function, this amount
///         is a flat, deployment-configured ratio -- not a formula scaled by the attacker/defender level gap
///         -- and is NOT multiplied by the sibling warrior-scroll (x2) or double-kill-exp-time (x8) buffs
///         the character-EXP amount receives (S07_MyGame03.cpp:3161-3188).
///     </para>
///     <para>
///         The level-113 (<c>LV_M1</c>) floor below which this amount is unconditionally zeroed, even with a
///         pet equipped, is the build-variant branch that is active in every real build (the source
///         contract's own build-variant note, S07_MyGame03.cpp:3176-3186) -- not a dead alternate-configuration
///         branch.
///     </para>
///     <para>
///         This method does NOT itself check whether the attacker has a pet equipped: that eligibility gate
///         (legacy <c>aEquip[EPET][0] &gt; 0 &amp;&amp; aEquip[EPET][1] &gt; 0</c>) collapses, in Fenrir, onto
///         the same "equipped pet item id resolves to a loaded, pet-categorized item definition" check
///         <see cref="Fenrir.Application.Game.Domain.Pets.PetExperienceCreditResolver.Resolve" /> already
///         performs for the monster-kill path -- reproducing it a second time here would just be the same
///         check under a different name. Callers pass the computed amount into the same resolver, which
///         silently credits nothing when no eligible pet is equipped, matching legacy's own "the shared call
///         only invokes the pet-system's crediting entry point when the forwarded amount is positive AND the
///         pet is eligible" behavior.
///     </para>
/// </remarks>
public static class PvpKillPetExperienceCalculator
{
    /// <summary>
    ///     <c>mGAME.mKillOtherTribePetExpRatio</c> (<c>Server/ts25zone/H07_MyGame.h:90</c>,
    ///     <c>Server/Header/api.h:155</c>), loaded from an ini key at server startup
    ///     (<c>Server/ts25zone/S07_MyGame01.cpp:453</c>, <c>Server/Header/ini.h:326</c>) -- a per-deployment
    ///     operator value, not a compile-time constant in the legacy source. No production number was plumbed
    ///     to this contract, so this is a placeholder, not real game-balance tuning -- an
    ///     operator-configurable option should replace this constant once the real deployment value is known.
    /// </summary>
    public const int PlaceholderPetExpRatio = 10;

    /// <summary><c>LV_M1</c> -- shared with <see cref="ExperienceFormulas.RebirthDivisorLevelThreshold" />.</summary>
    private const int LevelFloor = ExperienceFormulas.RebirthDivisorLevelThreshold;

    /// <summary>
    ///     <paramref name="petExpRatio" /> (defaults to <see cref="PlaceholderPetExpRatio" />) verbatim, UNLESS
    ///     <paramref name="attackerCombinedLevel" /> (legacy <c>aLevel1</c> == Level + Level2, never
    ///     RebirthCount -- the same combined-level definition every other sibling PvP-kill-reward calculator in
    ///     this namespace already uses) is below <see cref="LevelFloor" />, in which case the result is zero
    ///     regardless of the configured ratio.
    /// </summary>
    public static int ComputeGain(int attackerCombinedLevel, int petExpRatio = PlaceholderPetExpRatio)
    {
        return attackerCombinedLevel < LevelFloor ? 0 : petExpRatio;
    }
}
