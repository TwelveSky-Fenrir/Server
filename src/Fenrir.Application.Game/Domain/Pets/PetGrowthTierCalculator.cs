using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Pets;

public static class PetGrowthTierCalculator
{
    private static readonly FrozenDictionary<int, int> CategoryByItemId = new Dictionary<int, int>
    {
        [541] = 0, [542] = 0, [547] = 0, [560] = 0, [1002] = 0, [1003] = 0, [2140] = 0, [1004] = 0, [1005] = 0,
        [8202] = 0, [8203] = 0, [8204] = 0, [8205] = 0,
        [543] = 1, [544] = 1, [548] = 1, [561] = 1, [1006] = 1, [1007] = 1, [1008] = 1, [1009] = 1, [1010] = 1,
        [1011] = 1, [1452] = 1, [17052] = 1, [86819] = 1,
        [8206] = 1, [8207] = 1, [8208] = 1, [8209] = 1, [8210] = 1, [8211] = 1,
        [545] = 2, [549] = 2, [562] = 2, [1012] = 2, [1013] = 2, [1014] = 2, [1015] = 2, [17053] = 2, [86820] = 2,
        [8212] = 2, [8213] = 2, [8214] = 2, [8215] = 2,
        [546] = 3, [550] = 3, [1016] = 3, [1310] = 3, [1311] = 3, [1312] = 3, [2133] = 3, [2144] = 3, [2160] = 3,
        [17055] = 3, [17056] = 3, [17057] = 3,
        [8216] = 3
    }.ToFrozenDictionary();

    public static int ComputeTier(int petItemId, int growth)
    {
        if (growth < 1)
            return 0;

        if (!CategoryByItemId.TryGetValue(petItemId, out var categoryIndex))
            return 0;

        var cap = PetGrowthCaps.Values[categoryIndex];
        if (growth >= cap)
            return 4;

        var degree = growth * 100.0f / cap;
        return degree switch
        {
            < 25f => 0,
            < 50f => 1,
            < 75f => 2,
            _ => 3
        };
    }

    public static bool HasTierIncreased(int petItemId, int growthBeforeCredit, int growthAfterCredit)
    {
        return ComputeTier(petItemId, growthAfterCredit) > ComputeTier(petItemId, growthBeforeCredit);
    }
}
