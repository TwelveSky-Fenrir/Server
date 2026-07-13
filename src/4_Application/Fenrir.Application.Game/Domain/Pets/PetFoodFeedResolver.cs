using System.Collections.Frozen;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Pets;

/// <summary>
///     Resolves a pet-food bulk feed. For each requested unit it credits pet growth through
///     <see cref="PetExperienceCreditResolver.Resolve" /> along the positive grow-up path, stopping the moment
///     a unit produces no credit (the pet is already at its category ceiling, or the equipped pet is not a
///     pet-sort / not-categorised item), matching legacy <c>PETSYSTEM::ProcessForExperience</c>'s bulk loop
///     which breaks on the first zero-credit unit. The count of units that actually produced a positive credit
///     is the number of food units the caller must consume -- never more, never fewer.
///     <para>
///         The per-item grow-up step is derived from the food item id, not from the wire: 3 for 1491/8430,
///         1 for 1492/8429, 40 for 17042/17043. The "3% / 1% / 40%" labels are literally accurate only for the
///         old-pet tiers; the absolute per-unit credit is the category multiplier times the step (see
///         <see cref="PetExperienceCreditCalculator" />), which is identical for paired old/new categories.
///     </para>
/// </summary>
public static class PetFoodFeedResolver
{
    private static readonly FrozenDictionary<int, int> GrowUpStepByFoodItemId = new Dictionary<int, int>
    {
        [1491] = 3, [8430] = 3,
        [1492] = 1, [8429] = 1,
        [17042] = 40, [17043] = 40
    }.ToFrozenDictionary();

    public static bool IsPetFood(int itemId)
    {
        return GrowUpStepByFoodItemId.ContainsKey(itemId);
    }

    public static bool TryResolveGrowUpStep(int foodItemId, out int growUpStep)
    {
        return GrowUpStepByFoodItemId.TryGetValue(foodItemId, out growUpStep);
    }

    /// <param name="currentActivity">
    ///     The equipped pet's activity flag. The caller guards this at 1 or above before feeding (the pet-food
    ///     case never reaches the reactivation branch); it is forwarded to the credit resolver only for the
    ///     pet-sort eligibility check.
    /// </param>
    public static PetFoodFeedResult Resolve(int petItemId, int currentGrowth, int currentActivity, int foodItemId,
        int bulkCount, FrozenDictionary<int, ItemDefinition> itemsById)
    {
        if (!TryResolveGrowUpStep(foodItemId, out var growUpStep))
            return new PetFoodFeedResult(0, currentGrowth, false);

        var growth = currentGrowth;
        var unitsCredited = 0;
        var tierIncreased = false;

        for (var unit = 0; unit < bulkCount; unit++)
        {
            var credited = PetExperienceCreditResolver.Resolve(petItemId, growth, currentActivity, 0, itemsById,
                growUpStep);

            // Not a pet-sort / uncategorised pet, or already at the category ceiling: this unit yields no
            // credit, so the bulk loop stops (legacy breaks on the first zero-credit unit) and no further unit
            // is consumed.
            if (!credited.IsEligible || credited.CreditedAmount <= 0)
                break;

            growth = credited.NewGrowth;
            unitsCredited++;

            // Tier is monotonic in growth, so OR-ing the per-unit crossings equals "final tier > start tier".
            tierIncreased |= credited.TierIncreased;
        }

        return new PetFoodFeedResult(unitsCredited, growth, tierIncreased);
    }
}

/// <summary>
///     Outcome of a pet-food bulk feed: how many food units produced a positive credit (and must therefore be
///     consumed), the resulting accumulated grow value, and whether a growth-step tier was crossed (which is
///     what drives the pet-derived-ability recompute/broadcast).
/// </summary>
public readonly record struct PetFoodFeedResult(int UnitsCredited, int NewGrowth, bool TierIncreased);
