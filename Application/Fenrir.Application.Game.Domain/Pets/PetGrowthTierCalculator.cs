using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Pets;

/// <summary>
///     Port of <c>PETSYSTEM::ReturnGrowStep</c> (GameSystem_07_Pet.cpp:335-435) -- the tier boundary the
///     ability-recalculation broadcast fires on, checked once before and once after a credited amount is
///     applied (<c>PETSYSTEM::ProcessForExperience</c>, GameSystem_07_Pet.cpp:1961-1968).
/// </summary>
/// <remarks>
///     Deliberately a SEPARATE item-id -&gt; category table from <see cref="PetExperienceCreditCalculator" />'s:
///     the legacy source itself uses two disagreeing tables reading the same <see cref="PetGrowthCaps" />
///     array. Every item id this table places in category 0-3 that <see cref="PetExperienceCreditCalculator" />
///     instead places in the corresponding higher category (index+4) is evaluated here against exactly half
///     its real growth cap -- a genuine legacy inconsistency, reproduced faithfully rather than "fixed."
///     <c>ReturnGrowPercent</c> (:437 onward, a sibling routine in the same file) is out of this contract's
///     scope -- only the tier-crossing comparison via <c>ReturnGrowStep</c> feeds the broadcast decision at
///     the cited call site.
/// </remarks>
public static class PetGrowthTierCalculator
{
    private static readonly FrozenDictionary<int, int> CategoryByItemId = new Dictionary<int, int>
    {
        [541] = 0, [542] = 0, [547] = 0, [560] = 0, [1002] = 0, [1003] = 0, [2140] = 0, [1004] = 0, [1005] = 0,
        [543] = 1, [544] = 1, [548] = 1, [561] = 1, [1006] = 1, [1007] = 1, [1008] = 1, [1009] = 1, [1010] = 1,
        [1011] = 1, [1452] = 1, [17052] = 1, [86819] = 1,
        [545] = 2, [549] = 2, [562] = 2, [1012] = 2, [1013] = 2, [1014] = 2, [1015] = 2, [17053] = 2, [86820] = 2,
        [546] = 3, [550] = 3, [1016] = 3, [1310] = 3, [1311] = 3, [1312] = 3, [2133] = 3, [2144] = 3, [2160] = 3,
        [17055] = 3, [17056] = 3, [17057] = 3
    }.ToFrozenDictionary();

    /// <summary>
    ///     0-3 for a growth percentage in [0,25)/[25,50)/[50,75)/[75,100) of its category cap, 4 at/above the
    ///     cap ("100%+"), or 0 for an unrecognized item id or a growth counter below 1 -- matching
    ///     <c>ReturnGrowStep</c>'s own <c>pGrowUpValue &lt; 1</c> guard and <c>default: return 0.0f;</c>.
    /// </summary>
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

    /// <summary>Whether crediting moved the tier strictly upward -- the broadcast gate at :1963.</summary>
    public static bool HasTierIncreased(int petItemId, int growthBeforeCredit, int growthAfterCredit)
    {
        return ComputeTier(petItemId, growthAfterCredit) > ComputeTier(petItemId, growthBeforeCredit);
    }
}
