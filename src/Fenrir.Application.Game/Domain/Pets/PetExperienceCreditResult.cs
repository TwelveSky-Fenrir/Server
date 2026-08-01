namespace Fenrir.Application.Game.Domain.Pets;

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
