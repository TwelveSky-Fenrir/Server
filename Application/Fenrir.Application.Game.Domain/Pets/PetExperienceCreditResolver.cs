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
        FrozenDictionary<int, ItemDefinition> itemsById)
    {
        if (petItemId == 0 || !itemsById.TryGetValue(petItemId, out var definition) ||
            definition.Item.Sort != PetItemCategory)
            return PetExperienceCreditResult.Ineligible;

        var reactivationApplied = currentActivity < 1;
        var newActivity = reactivationApplied ? 1 : currentActivity;

        var creditedAmount =
            PetExperienceCreditCalculator.ComputeCreditedAmount(petItemId, currentGrowth, requestedPetExperience);
        var newGrowth = currentGrowth + creditedAmount;

        var tierIncreased = creditedAmount > 0 &&
                            PetGrowthTierCalculator.HasTierIncreased(petItemId, currentGrowth, newGrowth);

        return new PetExperienceCreditResult(true, reactivationApplied, newActivity, creditedAmount, newGrowth,
            tierIncreased);
    }
}
