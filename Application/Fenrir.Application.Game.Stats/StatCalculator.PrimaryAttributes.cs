using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    private static readonly int[] TitleTableA = [1, 3, 6, 10, 15, 21, 28, 36, 45, 55, 67, 82, 97, 112];
    private static readonly int[] TitleTableB = [0, 1, 3, 5, 8, 11, 14, 18, 23, 28, 34, 41, 48, 55];
    private static readonly int[] TitleTableC = [2, 6, 12, 20, 30, 42, 56, 72, 90, 110, 134, 164, 194, 224];
    private static readonly int[] TitleTableD = [1, 2, 3, 5, 7, 10, 14, 18, 22, 27, 33, 41, 49, 57];
    private static readonly int[] TitleTableE = [3, 8, 15, 25, 37, 52, 70, 90, 112, 137, 167, 205, 243, 281];


    private static readonly HashSet<int> CustomDecoStatItemExplicitIds =
    [
        594, 595, 596,
        1385, 1389, 1393,
        1483, 1484, 1485,
        2307, 2308, 2309,
        8010, 8011, 8012,
        91483, 91484, 91485, 91486, 91487, 91488
    ];


    private static int ComputeVitality(CharacterBaseAttributes attributes, EquippedItemSlot?[] bySlot,
        CosmeticContext cosmetic = default, ConsumableContext consumable = default, MountContext mount = default)
    {
        var total = attributes.Halo + attributes.Vitality;
        foreach (var slot in bySlot)
            if (slot is { } s)
                total += s.Item.Vitality;

        total += TitleVitalityBonus(attributes.Title);
        total += CustomDecoVitBonus(bySlot);
        total += RuneVitalityBonus(cosmetic);
        total += CostumeVitalityContribution(cosmetic.CostumeValue, cosmetic.CostumeEnchantCs);
        total += MountAbsorbPrimaryBonus(mount);

        return total;
    }

    private static int ComputeStrength(CharacterBaseAttributes attributes, EquippedItemSlot?[] bySlot,
        CosmeticContext cosmetic = default, ConsumableContext consumable = default, MountContext mount = default)
    {
        var total = attributes.Halo + attributes.Strength;
        foreach (var slot in bySlot)
            if (slot is { } s)
                total += s.Item.Strength;

        total += TitleStrengthBonus(attributes.Title);
        total += CustomDecoStrBonus(bySlot);
        total += RuneStrengthBonus(cosmetic);
        total += CostumeStrengthContribution(cosmetic.CostumeValue, cosmetic.CostumeEnchantCs);
        total += MountAbsorbPrimaryBonus(mount);

        return total;
    }

    private static int ComputeKi(CharacterBaseAttributes attributes, EquippedItemSlot?[] bySlot,
        CosmeticContext cosmetic = default, ConsumableContext consumable = default, MountContext mount = default)
    {
        var total = attributes.Halo + attributes.Intelligence;
        foreach (var slot in bySlot)
            if (slot is { } s)
                total += s.Item.Intelligent;

        total += TitleKiBonus(attributes.Title);
        total += CustomDecoIntBonus(bySlot);
        total += RuneKiBonus(cosmetic);
        total += CostumeKiContribution(cosmetic.CostumeValue,
            cosmetic.CostumeEnchantCs);
        total += MountAbsorbPrimaryBonus(mount);

        return total;
    }

    private static int ComputeWisdom(CharacterBaseAttributes attributes, EquippedItemSlot?[] bySlot,
        CosmeticContext cosmetic = default, ConsumableContext consumable = default, MountContext mount = default)
    {
        var total = attributes.Halo + attributes.Dexterity;
        foreach (var slot in bySlot)
            if (slot is { } s)
                total += s.Item.Dexterity;

        total += TitleWisdomBonus(attributes.Title);
        total += CustomDecoDexBonus(bySlot);
        total += RuneWisdomBonus(cosmetic);
        total += CostumeWisdomContribution(cosmetic.CostumeValue,
            cosmetic.CostumeEnchantCs);
        total += MountAbsorbPrimaryBonus(mount);

        return total;
    }


    private static int RuneVitalityBonus(CosmeticContext cosmetic)
    {
        var ids = cosmetic.RuneItemIds;
        var stats = cosmetic.RuneStatValues;
        if (ids.IsDefaultOrEmpty || stats.IsDefaultOrEmpty) return 0;

        var bonus = 0;
        var count = Math.Min(Math.Min(ids.Length, stats.Length), RuneStatDecoder.SocketCount);
        for (var i = 0; i < count; i++)
        {
            if (ids[i] <= 0) continue;
            var value = RuneStatDecoder.Vitality(stats[i]);
            if (value > 0) bonus += value;
        }

        return bonus;
    }

    private static int RuneStrengthBonus(CosmeticContext cosmetic)
    {
        var ids = cosmetic.RuneItemIds;
        var stats = cosmetic.RuneStatValues;
        if (ids.IsDefaultOrEmpty || stats.IsDefaultOrEmpty) return 0;

        var bonus = 0;
        var count = Math.Min(Math.Min(ids.Length, stats.Length), RuneStatDecoder.SocketCount);
        for (var i = 0; i < count; i++)
        {
            if (ids[i] <= 0) continue;
            var value = RuneStatDecoder.Strength(stats[i]);
            if (value > 0) bonus += value;
        }

        return bonus;
    }

    private static int RuneKiBonus(CosmeticContext cosmetic)
    {
        var ids = cosmetic.RuneItemIds;
        var stats = cosmetic.RuneStatValues;
        if (ids.IsDefaultOrEmpty || stats.IsDefaultOrEmpty) return 0;

        var bonus = 0;
        var count = Math.Min(Math.Min(ids.Length, stats.Length), RuneStatDecoder.SocketCount);
        for (var i = 0; i < count; i++)
        {
            if (ids[i] <= 0) continue;
            var value = RuneStatDecoder.Ki(stats[i]);
            if (value > 0) bonus += value;
        }

        return bonus;
    }

    private static int RuneWisdomBonus(CosmeticContext cosmetic)
    {
        var ids = cosmetic.RuneItemIds;
        var stats = cosmetic.RuneStatValues;
        if (ids.IsDefaultOrEmpty || stats.IsDefaultOrEmpty) return 0;

        var bonus = 0;
        var count = Math.Min(Math.Min(ids.Length, stats.Length), RuneStatDecoder.SocketCount);
        for (var i = 0; i < count; i++)
        {
            if (ids[i] <= 0) continue;
            var value = RuneStatDecoder.Dexterity(stats[i]);
            if (value > 0) bonus += value;
        }

        return bonus;
    }

    private static bool IsCustomDecoStatItem(int itemId)
    {
        return itemId is >= 101 and <= 151 || CustomDecoStatItemExplicitIds.Contains(itemId);
    }

    private static int CustomDecoVitBonus(EquippedItemSlot?[] bySlot)
    {
        var bonus = 0;
        for (var slotIndex = 9; slotIndex <= 12; slotIndex++)
            if (bySlot[slotIndex] is { } s && IsCustomDecoStatItem(s.Item.ItemId) && s.Item.Vitality < 100)
                bonus += 100 - s.Item.Vitality;
        return bonus;
    }

    private static int CustomDecoStrBonus(EquippedItemSlot?[] bySlot)
    {
        var bonus = 0;
        for (var slotIndex = 9; slotIndex <= 12; slotIndex++)
            if (bySlot[slotIndex] is { } s && IsCustomDecoStatItem(s.Item.ItemId) && s.Item.Strength < 100)
                bonus += 100 - s.Item.Strength;
        return bonus;
    }

    private static int CustomDecoIntBonus(EquippedItemSlot?[] bySlot)
    {
        var bonus = 0;
        for (var slotIndex = 9; slotIndex <= 12; slotIndex++)
            if (bySlot[slotIndex] is { } s && IsCustomDecoStatItem(s.Item.ItemId) && s.Item.Intelligent < 100)
                bonus += 100 - s.Item.Intelligent;
        return bonus;
    }

    private static int CustomDecoDexBonus(EquippedItemSlot?[] bySlot)
    {
        var bonus = 0;
        for (var slotIndex = 9; slotIndex <= 12; slotIndex++)
            if (bySlot[slotIndex] is { } s && IsCustomDecoStatItem(s.Item.ItemId) && s.Item.Dexterity < 100)
                bonus += 100 - s.Item.Dexterity;
        return bonus;
    }


    private static int TitleRankBonus(int[] table, int rank)
    {
        return rank is >= 1 and <= 14 ? table[rank - 1] : 0;
    }

    private static int TitleVitalityBonus(int title)
    {
        if (title <= 0) return 0;
        var rank = title % 100;
        var table = title switch
        {
            <= 100 => TitleTableA,
            <= 300 => TitleTableB,
            <= 400 => TitleTableC,
            _ => TitleTableB
        };
        return TitleRankBonus(table, rank);
    }

    private static int TitleStrengthBonus(int title)
    {
        if (title <= 0) return 0;
        var rank = title % 100;
        var table = title switch
        {
            <= 100 => TitleTableA,
            <= 200 => TitleTableC,
            <= 300 => TitleTableD,
            <= 400 => TitleTableB,
            _ => TitleTableA
        };
        return TitleRankBonus(table, rank);
    }

    private static int TitleKiBonus(int title)
    {
        if (title <= 0) return 0;
        var rank = title % 100;
        var table = title switch
        {
            <= 100 => TitleTableA,
            <= 200 => TitleTableA,
            <= 300 => TitleTableB,
            <= 400 => TitleTableA,
            _ => TitleTableC
        };
        return TitleRankBonus(table, rank);
    }

    private static int TitleWisdomBonus(int title)
    {
        if (title <= 0) return 0;
        var rank = title % 100;
        var table = title switch
        {
            <= 100 => TitleTableA,
            <= 200 => TitleTableD,
            <= 300 => TitleTableE,
            _ => TitleTableD
        };
        return TitleRankBonus(table, rank);
    }
}
