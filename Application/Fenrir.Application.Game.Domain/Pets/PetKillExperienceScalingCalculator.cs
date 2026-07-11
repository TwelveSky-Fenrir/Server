namespace Fenrir.Application.Game.Domain.Pets;

/// <summary>
///     Port of the global-ratio/personal-add-on/double-timer/premium pet-experience scaling chain a monster
///     kill's raw pet-experience payout goes through (<c>MONSTER_OBJECT::ProcessForExp</c>,
///     <c>S07_MyGame05.cpp:3855-3863</c>) before it ever reaches
///     <see cref="PetExperienceCreditResolver" />/<see cref="PetExperienceCreditCalculator" />'s own
///     cap-clamped crediting step -- both of which already document this exact chain as an out-of-scope,
///     "caller must pre-scale" input on their own remarks.
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ : <c>S07_MyGame05.cpp:3855-3863</c> (the four-step order of operations, read in full this
///         session: base value, x20 global ratio, +10% personal add-on, x2 double-EXP-timer, x2 premium).
///     </para>
///     <para>
///         <c>S07_MyGame01.cpp:437-438</c> (the global ratio hardcoded to exactly 20 at zone-process startup,
///         explicitly labeled a custom change in the source's own comment, overriding the "PetExpUpRatio" INI
///         read that <c>Server/Header/ini.h:315</c>/<c>Server/Header/api.h:144</c> perform but which is never
///         consulted again -- <see cref="GlobalRatio" /> models this permanent, process-lifetime-fixed value
///         directly rather than as a per-call config read). The ratio's own would-be weekend/date-driven
///         dynamic-adjustment mechanism (<c>S07_MyGame01.cpp:1674,1679,1912-1927</c>) is dead code (both call
///         sites commented out) and is not reproduced.
///     </para>
///     <para>
///         <c>S04_MyWork02.cpp:525-539</c> (the personal add-on ratio is only ever exactly 0 or exactly 0.1,
///         gated by one specific state-effect selector value on the character). Treated here as an opaque,
///         already-resolved caller input (<paramref name="personalAddOnRatio" /> below) per the contract's
///         own Inputs section -- no <see cref="Fenrir.Application.Game.Domain.World.PlayerRuntimeState" />
///         field models that selector yet, so callers should pass 0 until it lands (see this workstream's
///         openQuestions).
///         <c>H07_MyGame.h:79,810</c> confirms the global ratio and this personal ratio are two separate
///         field declarations, not the same field cited twice.
///     </para>
/// </remarks>
public static class PetKillExperienceScalingCalculator
{
    /// <summary>
    ///     The global pet-EXP ratio, hardcoded at zone-process startup and permanent for the life of the
    ///     process (never a per-call config read) -- see this type's own remarks.
    /// </summary>
    public const int GlobalRatio = 20;

    /// <summary>
    ///     Applies the full stacking chain to a monster's raw pet-experience payout, in the contract's
    ///     verified order: the global ratio, then the personal add-on (a flat addition of
    ///     <paramref name="personalAddOnRatio" /> times the already-x20'd amount -- NOT an independent
    ///     multiplier of the original base), then two independent doublings (double-EXP timer, premium
    ///     status). A character with both doublings active receives the single richest legacy case: 20x the
    ///     base, increased by 10%, then doubled twice -- 88x the monster's raw base value.
    /// </summary>
    /// <param name="baseAmount">The killed monster's own static pet-EXP reward (already resolved by the caller).</param>
    /// <param name="personalAddOnRatio">
    ///     The killing character's personal pet-EXP add-on ratio -- 0 (inactive, the default) or 0.1 (a flat
    ///     10 percent add-on) per the legacy source; no other value is ever assigned there, but this method
    ///     applies whatever fraction it is given verbatim.
    /// </param>
    /// <param name="doubleExpTimerActive">Whether the killing character's double-pet-EXP consumable timer is currently positive.</param>
    /// <param name="premiumActive">Whether the killing character's premium status is currently active.</param>
    public static int ComputeScaledAmount(
        int baseAmount,
        float personalAddOnRatio,
        bool doubleExpTimerActive,
        bool premiumActive)
    {
        if (baseAmount <= 0)
            return 0;

        var scaled = baseAmount;

        if (GlobalRatio > 1)
            scaled *= GlobalRatio;

        if (personalAddOnRatio > 0f)
            scaled += (int)(scaled * personalAddOnRatio);

        if (doubleExpTimerActive)
            scaled *= 2;

        if (premiumActive)
            scaled *= 2;

        return scaled;
    }
}
