using System.Collections.Frozen;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Pets;

public static class PetExperienceCreditResolver
{
    private const byte PetItemCategory = 22;

    public static PetExperienceCreditResult Resolve(
        int petItemId,
        int currentGrowth,
        int currentActivity,
        int requestedPetExperience,
        FrozenDictionary<int, ItemDefinition> itemsById,
        float growUpValue = 0f)
    {
        if (petItemId == 0 || !itemsById.TryGetValue(petItemId, out var definition) ||
            definition.Item.Sort != PetItemCategory)
            return PetExperienceCreditResult.Ineligible;

        var reactivationApplied = currentActivity < 1;
        var newActivity = reactivationApplied ? 1 : currentActivity;

        // growUpValue defaults to 0 (kill / experience-distribution path: seed credited directly). A pet-food
        // or GM-fill caller passes the item's positive grow-up step (1 / 3 / 40 / 200) to scale by category.
        var creditedAmount =
            PetExperienceCreditCalculator.ComputeCreditedAmount(petItemId, currentGrowth, requestedPetExperience,
                growUpValue);
        var newGrowth = currentGrowth + creditedAmount;

        var tierIncreased = creditedAmount > 0 &&
                            PetGrowthTierCalculator.HasTierIncreased(petItemId, currentGrowth, newGrowth);

        return new PetExperienceCreditResult(true, reactivationApplied, newActivity, creditedAmount, newGrowth,
            tierIncreased);
    }
}
