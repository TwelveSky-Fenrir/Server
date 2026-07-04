using System.Collections.Frozen;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Pets;

/// <summary>
///     Pure port of the ACTIVE (non-<c>USE_OLD_PET</c> -- that define is never set anywhere in DEFINE.h, so
///     the whole <c>#ifdef USE_OLD_PET</c> block of <c>GameSystem_07_Pet.cpp</c> lines 27-333 is dead code)
///     <c>PETSYSTEM::ReturnLifeValue/ReturnManaValue/ReturnAttackPower/ReturnDefensePower</c>
///     (<c>Server/ts25zone/GameSystem/GameSystem_07_Pet.cpp:532-694,1578-1720</c>, read in full and verified
///     byte-for-byte -- <c>GIFT_EVENT</c> is unconditionally <c>#define</c>d, DEFINE.h:117, so its extra
///     item ids are always live). Populates <see cref="PetStatContribution" /> from
///     <see cref="Fenrir.Application.Game.World.PlayerRuntimeState.PetGrowth" />/<see cref="Fenrir.Application.Game.World.PlayerRuntimeState.PetActivity" />
///     -- see <see cref="Stats.StatCalculator" />'s own class remarks for why populating this parameter
///     (not modifying <see cref="StatCalculator" /> itself) is the whole job here.
/// </summary>
/// <remarks>
///     IMPORTANT, source-verified nuance: activity gates ONLY <see cref="ComputeAttackPower" /> (the
///     legacy explicitly checks <c>pActivityValue &lt; 1</c>) -- Life/Mana/Defense do NOT reference the
///     activity parameter at all in their active bodies. A prior draft of this pass assumed activity gated
///     all four uniformly; corrected against the actual source read.
///     <para>
///     Each of the 4 functions has ITS OWN item-id-&gt;family membership (verified: the 4 switch-case
///     blocks differ from each other, not just one shared table reused 4 times) -- kept as 4 separate
///     frozen sets rather than one merged table to avoid inventing a false "these must be the same"
///     simplification.
///     </para>
/// </remarks>
public static class PetGrowthCalculator
{
    /// <summary><c>mMaxRangeValue[0..3]</c> (PETSYSTEM constructor) -- the only 4 indices these 4 functions ever select (indices 4-7 are new-pet variants of <see cref="Quests.QuestStateMachine" />-unrelated systems this pass does not touch).</summary>
    private static readonly int[] MaxRangeValue = [40_000_000, 80_000_000, 160_000_000, 320_000_000];

    private static readonly FrozenDictionary<int, int> LifeFamily = BuildFamily(
        [1004, 8204],
        [544, 1007, 1009, 1011, 8207, 8209, 8211],
        [545, 549, 562, 1012, 1014, 1015, 17053, 86820, 8212, 8214, 8215],
        [546, 550, 1016, 1310, 1311, 1312, 2133, 2144, 2160, 17055, 17056, 17057, 8216]);

    private static readonly FrozenSet<int> LifePremium = new HashSet<int> { 1310, 17055 }.ToFrozenSet();

    private static readonly FrozenDictionary<int, int> ManaFamily = BuildFamily(
        [1005, 8205],
        [1008, 1010, 1011, 8208, 8210, 8211],
        [1013, 1014, 1015, 8213, 8214, 8215],
        [546, 550, 1016, 1310, 1311, 1312, 2133, 2144, 2160, 17055, 17056, 17057, 8216]);

    private static readonly FrozenSet<int> ManaPremium = LifePremium;

    private static readonly FrozenDictionary<int, int> AttackFamily = BuildFamily(
        [541, 560, 1002, 8202],
        [543, 544, 548, 561, 1006, 1007, 1008, 1452, 17052, 86819, 8206, 8207, 8208],
        [545, 549, 562, 1012, 1013, 1014, 17053, 86820, 8212, 8213, 8214],
        [546, 550, 1016, 1310, 1311, 1312, 2133, 2144, 2160, 17055, 17056, 17057, 8216]);

    private static readonly FrozenSet<int> AttackPremium = new HashSet<int> { 1312, 17057, 2133, 2144, 2160 }
        .ToFrozenSet();

    private static readonly FrozenDictionary<int, int> DefenseFamily = BuildFamily(
        [542, 547, 1003, 2140, 8203],
        [543, 548, 561, 1006, 1009, 1010, 1452, 17052, 86819, 8206, 8209, 8210],
        [545, 549, 562, 1012, 1013, 1015, 17053, 86820, 8212, 8213, 8215],
        [546, 550, 1016, 1310, 1311, 1312, 2133, 2144, 2160, 17055, 17056, 17057, 8216]);

    private static readonly FrozenSet<int> DefensePremium = new HashSet<int> { 1311, 17056 }.ToFrozenSet();

    /// <summary>
    ///     Resolves the live <see cref="PetStatContribution" /> for whatever is currently equipped in the
    ///     pet slot (<see cref="PetSlots.EquipmentSlot" />) -- returns <c>default</c> (all zeros, the
    ///     "no pet"/"not a growable pet"/"no growth yet" case) for anything that isn't a real <c>iSort==22</c>
    ///     pet item, exactly like an absent legacy feature would.
    /// </summary>
    public static PetStatContribution Compute(int petItemId, int growth, int activity,
        FrozenDictionary<int, ItemDefinition> itemsById)
    {
        if (petItemId == 0 || growth < 1)
            return default;

        if (!itemsById.TryGetValue(petItemId, out var definition) || definition.Item.Sort != 22)
            return default;

        return new PetStatContribution(
            ComputeTiered(petItemId, growth, LifeFamily, LifePremium, 2000f, 2200f, 4000f, 4400f),
            ComputeTiered(petItemId, growth, ManaFamily, ManaPremium, 1800f, 2000f, 3600f, 4000f),
            activity < 1 ? 0 : ComputeTiered(petItemId, growth, AttackFamily, AttackPremium, 1000f, 1100f, 2000f, 2200f),
            ComputeTiered(petItemId, growth, DefenseFamily, DefensePremium, 2000f, 2200f, 4000f, 4400f));
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
