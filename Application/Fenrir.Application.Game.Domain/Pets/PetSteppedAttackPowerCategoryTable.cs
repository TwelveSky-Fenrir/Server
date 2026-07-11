using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Pets;

/// <summary>
///     Port of <c>PETSYSTEM::ReturnAttackPower2</c>'s own item-id -&gt; category table
///     (<c>GameSystem_07_Pet.cpp:1722-1802</c>), resolving the tier-maximum growth ceiling that
///     <see cref="Fenrir.Application.Game.Stats.StatCalculator.PetSteppedAttackBonus" /> needs for its
///     <c>tierMax</c> parameter.
/// </summary>
/// <remarks>
///     <para>
///         Deliberately a strictly smaller, non-identical subset of <see cref="DoublePetExpTimerGate" />'s
///         own Table A (the double-pet-EXP-timer freeze gate) -- the B8-pet-growth-depth contract's own
///         re-verification (both switch statements read in full) found several ids present in Table A
///         (541, 542, 547, 560, 543, 544, 548, 561, 1452, 86819, 545, 549, 562, 86820, 546, 550) wholly
///         absent here: a pet built from any of those ids can reach and be gated at 200% growth by the timer
///         gate while permanently receiving a flatly-zero six-tier attack bonus, because it falls through to
///         this function's own default branch. This is genuine, verified legacy behavior -- do not merge the
///         two tables.
///     </para>
///     <para>
///         Includes the GIFT_EVENT-gated 8200-series ids, re-verified live under both real build
///         configurations by this contract (see <see cref="DoublePetExpTimerGate" />'s own remarks for the
///         full macro-chain citation, which applies identically here).
///     </para>
/// </remarks>
public static class PetSteppedAttackPowerCategoryTable
{
    private static readonly FrozenDictionary<int, int> CategoryByItemId = new Dictionary<int, int>
    {
        // Category 0: legacy ids + GIFT_EVENT 8202-8205.
        [1002] = 0, [1003] = 0, [1004] = 0, [1005] = 0, [2140] = 0,
        [8202] = 0, [8203] = 0, [8204] = 0, [8205] = 0,

        // Category 1: legacy ids + GIFT_EVENT 8206-8211.
        [1006] = 1, [1007] = 1, [1008] = 1, [1009] = 1, [1010] = 1, [1011] = 1, [17052] = 1,
        [8206] = 1, [8207] = 1, [8208] = 1, [8209] = 1, [8210] = 1, [8211] = 1,

        // Category 2: legacy ids + GIFT_EVENT 8212-8215.
        [1012] = 2, [1013] = 2, [1014] = 2, [1015] = 2, [17053] = 2,
        [8212] = 2, [8213] = 2, [8214] = 2, [8215] = 2,

        // Category 3: legacy ids + GIFT_EVENT 8216.
        [1016] = 3, [1310] = 3, [1311] = 3, [1312] = 3, [2133] = 3, [2144] = 3, [2160] = 3,
        [17055] = 3, [17056] = 3, [17057] = 3,
        [8216] = 3
    }.ToFrozenDictionary();

    /// <summary>
    ///     Resolves the <see cref="PetGrowthCaps" /> tier maximum (indices 0-3) for
    ///     <paramref name="petItemId" />, or false when the id doesn't appear in this function's own category
    ///     table -- the contract's own edge case: the six-tier bonus is then flatly zero, independent of
    ///     growth or activity.
    /// </summary>
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
