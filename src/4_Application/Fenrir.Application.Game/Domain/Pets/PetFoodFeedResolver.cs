using System.Collections.Frozen;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Pets;

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

            if (!credited.IsEligible || credited.CreditedAmount <= 0)
                break;

            growth = credited.NewGrowth;
            unitsCredited++;

            tierIncreased |= credited.TierIncreased;
        }

        return new PetFoodFeedResult(unitsCredited, growth, tierIncreased);
    }
}

public readonly record struct PetFoodFeedResult(int UnitsCredited, int NewGrowth, bool TierIncreased);
