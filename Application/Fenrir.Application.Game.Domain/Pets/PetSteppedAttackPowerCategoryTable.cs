using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Pets;

public static class PetSteppedAttackPowerCategoryTable
{
    private static readonly FrozenDictionary<int, int> CategoryByItemId = new Dictionary<int, int>
    {
        [1002] = 0, [1003] = 0, [1004] = 0, [1005] = 0, [2140] = 0,
        [8202] = 0, [8203] = 0, [8204] = 0, [8205] = 0,

        [1006] = 1, [1007] = 1, [1008] = 1, [1009] = 1, [1010] = 1, [1011] = 1, [17052] = 1,
        [8206] = 1, [8207] = 1, [8208] = 1, [8209] = 1, [8210] = 1, [8211] = 1,

        [1012] = 2, [1013] = 2, [1014] = 2, [1015] = 2, [17053] = 2,
        [8212] = 2, [8213] = 2, [8214] = 2, [8215] = 2,

        [1016] = 3, [1310] = 3, [1311] = 3, [1312] = 3, [2133] = 3, [2144] = 3, [2160] = 3,
        [17055] = 3, [17056] = 3, [17057] = 3,
        [8216] = 3
    }.ToFrozenDictionary();

        public static bool TryResolveTierMax(int petItemId, out int tierMax)
    {
        if (CategoryByItemId.TryGetValue(petItemId, out var categoryIndex))
        {
            tierMax = PetGrowthCaps.Values[categoryIndex];
            return true;
        }

        tierMax = 0;
        return false;
    }
}
