using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Pets;

/// <summary>
///     Port of <c>PETSYSTEM::ReturnExperience</c> (GameSystem_07_Pet.cpp:1804-1918) for the monster-kill call
///     shape only: <c>MyUtil::ProcessForExperience</c> (S07_MyGame03.cpp:320) always calls
///     <c>PETSYSTEM::ProcessForExperience</c> with its <c>pGrowUpValue</c> percent-rate parameter fixed at
///     <c>0.0f</c>, which skips the whole percent-of-cap recompute branch (:1908-1912) and leaves the
///     requested amount untouched except for the cap clamp (:1899-1907, :1913-1917) -- that is the only path
///     this class models. The nonzero-<c>pGrowUpValue</c> percent path (the sibling, non-monster-kill
///     <c>MyWork::TimeExchange</c> play-time-event trigger, S04_MyWork05.cpp:4808-4826) is out of this
///     contract's scope and is not reproduced here.
/// </summary>
public static class PetExperienceCreditCalculator
{
    /// <summary>
    ///     Item-id -&gt; <see cref="PetGrowthCaps" /> index table, GameSystem_07_Pet.cpp:1811-1898 (the
    ///     <c>PETSYSTEM::ReturnExperience</c> switch). The <c>GIFT_EVENT</c>-gated 8200s ids inside that
    ///     switch are never compiled in any build (macro never defined anywhere in <c>Server/</c>) and are
    ///     deliberately omitted here.
    /// </summary>
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

    /// <summary>Whether <paramref name="petItemId" /> matches any entry in the crediting category table.</summary>
    public static bool TryResolveCategory(int petItemId, out int categoryIndex)
    {
        return CategoryByItemId.TryGetValue(petItemId, out categoryIndex);
    }

    /// <summary>
    ///     The amount of pet experience to actually credit: 0 if the item id is unrecognized or the growth
    ///     counter already meets/exceeds its category's cap; otherwise <paramref name="requestedAmount" />,
    ///     reduced if necessary so the growth counter lands exactly on the cap and never above it.
    /// </summary>
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
