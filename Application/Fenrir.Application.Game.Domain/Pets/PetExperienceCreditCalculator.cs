using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Pets;

public static class PetExperienceCreditCalculator
{
    private static readonly FrozenDictionary<int, int> CategoryByItemId = new Dictionary<int, int>
    {
        [541] = 0, [542] = 0, [547] = 0, [560] = 0,
        [543] = 1, [544] = 1, [548] = 1, [561] = 1, [1452] = 1, [86819] = 1,
        [545] = 2, [549] = 2, [562] = 2, [86820] = 2,
        [546] = 3, [550] = 3,
        [1002] = 4, [1003] = 4, [2140] = 4, [1004] = 4, [1005] = 4,
        [1006] = 5, [1007] = 5, [1008] = 5, [1009] = 5, [1010] = 5, [1011] = 5, [17052] = 5,
        [1012] = 6, [1013] = 6, [1014] = 6, [1015] = 6, [17053] = 6,
        [1016] = 7, [1310] = 7, [1311] = 7, [1312] = 7, [2133] = 7, [2144] = 7, [2160] = 7,
        [17055] = 7, [17056] = 7, [17057] = 7
    }.ToFrozenDictionary();

    public static bool TryResolveCategory(int petItemId, out int categoryIndex)
    {
        return CategoryByItemId.TryGetValue(petItemId, out categoryIndex);
    }

    public static int ComputeCreditedAmount(int petItemId, int currentGrowth, int requestedAmount)
    {
        if (requestedAmount <= 0)
            return 0;

        if (!TryResolveCategory(petItemId, out var categoryIndex))
            return 0;

        var cap = PetGrowthCaps.Values[categoryIndex];
        if (currentGrowth >= cap)
            return 0;

        var projected = currentGrowth + requestedAmount;
        return projected > cap ? cap - currentGrowth : requestedAmount;
    }
}
