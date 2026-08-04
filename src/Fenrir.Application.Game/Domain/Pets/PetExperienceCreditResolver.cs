using System.Collections.Frozen;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.Pets;

public static class PetExperienceCreditResolver
{
    private const byte PetItemCategory = 22;

    public static PetExperienceCreditResult ResolveWithZoneMultiplier(
        int petItemId,
        int currentGrowth,
        int currentActivity,
        int requestedPetExperience,
        float zoneExperienceMultiplier,
        FrozenDictionary<int, ItemDefinition> itemsById,
        float growUpValue = 0f)
    {
        if (!float.IsFinite(zoneExperienceMultiplier) || zoneExperienceMultiplier <= 0f)
            return PetExperienceCreditResult.Ineligible;

        var scaledExperience = ApplyZoneMultiplier(requestedPetExperience, zoneExperienceMultiplier);
        return Resolve(petItemId, currentGrowth, currentActivity, scaledExperience, itemsById, growUpValue);
    }

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

        if (currentActivity < 1)
            return PetExperienceCreditResult.Ineligible;

        var creditedAmount =
            PetExperienceCreditCalculator.ComputeCreditedAmount(petItemId, currentGrowth, requestedPetExperience,
                growUpValue);
        var newGrowth = currentGrowth + creditedAmount;

        var tierIncreased = creditedAmount > 0 &&
                            PetGrowthTierCalculator.HasTierIncreased(petItemId, currentGrowth, newGrowth);

        return new PetExperienceCreditResult(true, false, currentActivity, creditedAmount, newGrowth,
            tierIncreased);
    }

    private static int ApplyZoneMultiplier(int requestedExperience, float multiplier)
    {
        if (requestedExperience <= 0)
            return requestedExperience;

        var scaled = requestedExperience * (double)multiplier;
        return scaled >= int.MaxValue ? int.MaxValue : (int)scaled;
    }
}
