using System.Collections.Frozen;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Pets;

/// <summary>
///     Port of PETSYSTEM::ReturnLifeValue/ReturnManaValue/ReturnAttackPower/ReturnDefensePower
///     (GameSystem_07_Pet.cpp:532-694,1578-1720). Populates <see cref="PetStatContribution" /> from the
///     player's pet growth/activity state.
/// </summary>
/// <remarks>Activity gates only AttackPower (source-verified); Life/Mana/Defense ignore it.</remarks>
public static class PetGrowthCalculator
{
    /// <summary>mMaxRangeValue[0..3] from the PETSYSTEM constructor.</summary>
    private static readonly int[] MaxRangeValue = [40_000_000, 80_000_000, 160_000_000, 320_000_000];

    /// <summary>
    ///     Item-id -&gt; tier table for <c>PETSYSTEM::ReturnLifeValue</c> (GameSystem_07_Pet.cpp:1583-1634). The
    ///     <c>GIFT_EVENT</c>-gated 8200s ids (8204 tier 0; 8207/8209/8211 tier 1; 8212/8214/8215 tier 2; 8216
    ///     tier 3) are never compiled in any build (macro never defined anywhere in <c>Server/</c>) and are
    ///     deliberately omitted here.
    /// </summary>
    private static readonly FrozenDictionary<int, int> LifeFamily = BuildFamily(
        [1004],
        [544, 1007, 1009, 1011],
        [545, 549, 562, 1012, 1014, 1015, 17053, 86820],
        [546, 550, 1016, 1310, 1311, 1312, 2133, 2144, 2160, 17055, 17056, 17057]);

    private static readonly FrozenSet<int> LifePremium = new HashSet<int> { 1310, 17055 }.ToFrozenSet();

    /// <summary>
    ///     Item-id -&gt; tier table for <c>PETSYSTEM::ReturnManaValue</c> (GameSystem_07_Pet.cpp:1658-1703). The
    ///     <c>GIFT_EVENT</c>-gated 8200s ids (8205 tier 0; 8208/8210/8211 tier 1; 8213/8214/8215 tier 2; 8216
    ///     tier 3) are never compiled in any build (macro never defined anywhere in <c>Server/</c>) and are
    ///     deliberately omitted here.
    /// </summary>
    private static readonly FrozenDictionary<int, int> ManaFamily = BuildFamily(
        [1005],
        [1008, 1010, 1011],
        [1013, 1014, 1015],
        [546, 550, 1016, 1310, 1311, 1312, 2133, 2144, 2160, 17055, 17056, 17057]);

    private static readonly FrozenSet<int> ManaPremium = LifePremium;

    /// <summary>
    ///     Item-id -&gt; tier table for <c>PETSYSTEM::ReturnAttackPower</c> (GameSystem_07_Pet.cpp:539-597). The
    ///     <c>GIFT_EVENT</c>-gated 8200s ids (8202 tier 0; 8206/8207/8208 tier 1; 8212/8213/8214 tier 2; 8216
    ///     tier 3) are never compiled in any build (macro never defined anywhere in <c>Server/</c>) and are
    ///     deliberately omitted here.
    /// </summary>
    private static readonly FrozenDictionary<int, int> AttackFamily = BuildFamily(
        [541, 560, 1002],
        [543, 544, 548, 561, 1006, 1007, 1008, 1452, 17052, 86819],
        [545, 549, 562, 1012, 1013, 1014, 17053, 86820],
        [546, 550, 1016, 1310, 1311, 1312, 2133, 2144, 2160, 17055, 17056, 17057]);

    private static readonly FrozenSet<int> AttackPremium = new HashSet<int> { 1312, 17057, 2133, 2144, 2160 }
        .ToFrozenSet();

    /// <summary>
    ///     Item-id -&gt; tier table for <c>PETSYSTEM::ReturnDefensePower</c> (GameSystem_07_Pet.cpp:620-679). The
    ///     <c>GIFT_EVENT</c>-gated 8200s ids (8203 tier 0; 8206/8209/8210 tier 1; 8212/8213/8215 tier 2; 8216
    ///     tier 3) are never compiled in any build (macro never defined anywhere in <c>Server/</c>) and are
    ///     deliberately omitted here.
    /// </summary>
    private static readonly FrozenDictionary<int, int> DefenseFamily = BuildFamily(
        [542, 547, 1003, 2140],
        [543, 548, 561, 1006, 1009, 1010, 1452, 17052, 86819],
        [545, 549, 562, 1012, 1013, 1015, 17053, 86820],
        [546, 550, 1016, 1310, 1311, 1312, 2133, 2144, 2160, 17055, 17056, 17057]);

    private static readonly FrozenSet<int> DefensePremium = new HashSet<int> { 1311, 17056 }.ToFrozenSet();

    /// <summary>Returns default (all zeros) for anything that isn't a real iSort==22 pet item.</summary>
    public static PetStatContribution Compute(int petItemId, int growth, int activity,
        FrozenDictionary<int, ItemDefinition> itemsById)
    {
        if (petItemId == 0 || growth < 1)
            return default;

        if (!itemsById.TryGetValue(petItemId, out var definition) || definition.Item.Sort != 22)
            return default;

        // B8-pet-growth-depth: PETSYSTEM::ReturnAttackPower2's own item-id -> category table
        // (PetSteppedAttackPowerCategoryTable) is a strictly smaller, non-identical subset of the four
        // families above -- an unresolved id here correctly yields 0, independent of growth/activity.
        var steppedAttackBonus = PetSteppedAttackPowerCategoryTable.TryResolveTierMax(petItemId, out var steppedTierMax)
            ? StatCalculator.PetSteppedAttackBonus(growth, steppedTierMax, activity)
            : 0;

        return new PetStatContribution(
            ComputeTiered(petItemId, growth, LifeFamily, LifePremium, 2000f, 2200f, 4000f, 4400f),
            ComputeTiered(petItemId, growth, ManaFamily, ManaPremium, 1800f, 2000f, 3600f, 4000f),
            activity < 1
                ? 0
                : ComputeTiered(petItemId, growth, AttackFamily, AttackPremium, 1000f, 1100f, 2000f, 2200f),
            ComputeTiered(petItemId, growth, DefenseFamily, DefensePremium, 2000f, 2200f, 4000f, 4400f),
            steppedAttackBonus);
    }

    private static int ComputeTiered(int petItemId, int growth, FrozenDictionary<int, int> family,
        FrozenSet<int> premiumIds, float normalK, float normalCap, float premiumK, float premiumCap)
    {
        if (!family.TryGetValue(petItemId, out var familyIndex))
            return 0;

        var max = MaxRangeValue[familyIndex];
        var (k, cap) = premiumIds.Contains(petItemId) ? (premiumK, premiumCap) : (normalK, normalCap);

        return growth < max ? (int)(growth * k / max) : (int)cap;
    }

    private static FrozenDictionary<int, int> BuildFamily(int[] family0, int[] family1, int[] family2, int[] family3)
    {
        var map = new Dictionary<int, int>();
        foreach (var id in family0) map[id] = 0;
        foreach (var id in family1) map[id] = 1;
        foreach (var id in family2) map[id] = 2;
        foreach (var id in family3) map[id] = 3;
        return map.ToFrozenDictionary();
    }
}
