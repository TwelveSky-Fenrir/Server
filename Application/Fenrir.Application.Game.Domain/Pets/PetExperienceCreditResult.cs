namespace Fenrir.Application.Game.Domain.Pets;

/// <summary>
///     Pure outcome of one <see cref="PetExperienceCreditResolver.Resolve" /> call -- mirrors
///     <c>PETSYSTEM::ProcessForExperience</c>'s (GameSystem_07_Pet.cpp:1920-1971) in-memory state changes and
///     which notifications it would send, without performing any of them: the integrator applies
///     <see cref="NewActivity" />/<see cref="NewGrowth" /> to the avatar's own state and sends whichever
///     notifications the flags below call for.
/// </summary>
/// <param name="IsEligible">
///     False when the equipped item id doesn't resolve to a loaded item definition, or resolves to one not
///     categorized as the pet item category (GameSystem_07_Pet.cpp:1926-1934) -- every other field is
///     meaningless when false; nothing changes and nothing is sent.
/// </param>
/// <param name="ReactivationApplied">
///     True when the pet was inactive (activity &lt; 1) and was unconditionally reactivated as the first
///     step (:1935-1951) -- happens regardless of whether any experience is credited below. When true, the
///     integrator recomputes derived stats and broadcasts a generic ability-recalculation notification.
/// </param>
/// <param name="NewActivity">The activity counter to persist -- unchanged unless <see cref="ReactivationApplied" />.</param>
/// <param name="CreditedAmount">
///     The actual amount of growth credited, already capped (see <see cref="PetExperienceCreditCalculator" />)
///     -- may be 0 (unrecognized category, or already at cap) even when <see cref="IsEligible" /> is true.
///     When positive, the integrator also sends an experience-changed notification carrying
///     <see cref="NewGrowth" /> to the credited avatar's own connection.
/// </param>
/// <param name="NewGrowth">The growth counter to persist -- <c>currentGrowth + CreditedAmount</c>.</param>
/// <param name="TierIncreased">
///     True only when <see cref="CreditedAmount" /> is positive AND the growth tier increased
///     (<see cref="PetGrowthTierCalculator" />) -- when true, the integrator recomputes derived stats and
///     broadcasts a generic ability-recalculation notification a second time, independent of (and possibly
///     in addition to) the one <see cref="ReactivationApplied" /> already triggered.
/// </param>
public readonly record struct PetExperienceCreditResult(
    bool IsEligible,
    bool ReactivationApplied,
    int NewActivity,
    int CreditedAmount,
    int NewGrowth,
    bool TierIncreased)
{
    public static readonly PetExperienceCreditResult Ineligible = new(false, false, 0, 0, 0, false);
}
