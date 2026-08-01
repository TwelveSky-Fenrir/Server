using System.Collections.Frozen;
using Fenrir.Domain.Game.Stats.Context;

namespace Fenrir.Domain.Game.Stats;

public static partial class StatCalculator
{
    public enum OrnamentSort
    {
        Hp = 0,

        Mp = 1,

        Dmg = 2,

        Def = 3
    }

    public enum OrnamentTier
    {
        NotActive = 0,

        Gold = 1,

        Silver = 2
    }

    public static readonly FrozenDictionary<OrnamentSort, OrnamentBonusRow> OrnamentBonusTable =
        new Dictionary<OrnamentSort, OrnamentBonusRow>
        {
            [OrnamentSort.Hp] = new(825, 550),
            [OrnamentSort.Mp] = new(750, 500),
            [OrnamentSort.Dmg] = new(413, 215),
            [OrnamentSort.Def] = new(825, 550)
        }.ToFrozenDictionary();

    public static readonly FrozenSet<int> OrnamentDecorationSlots = new[] { 9, 10, 11, 12 }.ToFrozenSet();

    public static OrnamentTier ResolveOrnamentTier(ZoneContext zone, EquippedItemSlot?[] bySlot)
    {
        if (!zone.OrnamentInUse)
            return OrnamentTier.NotActive;

        foreach (var slotIndex in OrnamentDecorationSlots)
            if (bySlot[slotIndex] is not { } slot || slot.Item.ItemId <= 0)
                return OrnamentTier.NotActive;

        if (zone.OrnamentGoldTimeRemaining > 0)
            return OrnamentTier.Gold;
        if (zone.OrnamentSilverTimeRemaining > 0)
            return OrnamentTier.Silver;
        return OrnamentTier.NotActive;
    }

    public static int GetOrnamentBonus(OrnamentSort sort, OrnamentTier tier)
    {
        if (!OrnamentBonusTable.TryGetValue(sort, out var row))
            return 0;

        return tier switch
        {
            OrnamentTier.Gold => row.Gold,
            OrnamentTier.Silver => row.Silver,
            _ => 0
        };
    }

    public static int OrnamentLifeContribution(ZoneContext zone, EquippedItemSlot?[] bySlot)
    {
        return GetOrnamentBonus(OrnamentSort.Hp, ResolveOrnamentTier(zone, bySlot));
    }

    public static int OrnamentManaContribution(ZoneContext zone, EquippedItemSlot?[] bySlot)
    {
        return GetOrnamentBonus(OrnamentSort.Mp, ResolveOrnamentTier(zone, bySlot));
    }

    public static int OrnamentAttackContribution(ZoneContext zone, EquippedItemSlot?[] bySlot)
    {
        return GetOrnamentBonus(OrnamentSort.Dmg, ResolveOrnamentTier(zone, bySlot));
    }

    public static int OrnamentDefenseContribution(ZoneContext zone, EquippedItemSlot?[] bySlot)
    {
        return GetOrnamentBonus(OrnamentSort.Def, ResolveOrnamentTier(zone, bySlot));
    }

    public readonly record struct OrnamentBonusRow(int Gold, int Silver);
}
