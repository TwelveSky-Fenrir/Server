using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // The 5 title-rank vectors (rank 1..14), reused across the 4 base stats' own tranche tables.
    private static readonly int[] TitleTableA = [1, 3, 6, 10, 15, 21, 28, 36, 45, 55, 67, 82, 97, 112];
    private static readonly int[] TitleTableB = [0, 1, 3, 5, 8, 11, 14, 18, 23, 28, 34, 41, 48, 55];
    private static readonly int[] TitleTableC = [2, 6, 12, 20, 30, 42, 56, 72, 90, 110, 134, 164, 194, 224];
    private static readonly int[] TitleTableD = [1, 2, 3, 5, 7, 10, 14, 18, 22, 27, 33, 41, 49, 57];
    private static readonly int[] TitleTableE = [3, 8, 15, 25, 37, 52, 70, 90, 112, 137, 167, 205, 243, 281];

    // ---- Custom-decoration-item primary-stat conversion bonus ----
    // Server/Header/Protocol/MyFactor.cpp:150-273 (MyFactor_IsCustomDecoStatItem + the 4 sibling
    // MyFactor_GetCustomDeco{Str,Vit,Int,Dex}Bonus helpers); unconditional tail calls at
    // GetBaseVitality:1710, GetBaseKi:1769, GetBaseStrength:1828, GetBaseWisdom:1887. Additive on top of
    // the ordinary per-slot item-stat sum above (both read the same 4 decoration slots) -- not a
    // replacement for it: a qualifying item's own low raw stat is counted once via the sum above, then a
    // second time here as (100 - rawStat).

    /// <summary>Item ids 594-596/1385/1389/1393/1483-1485/2307-2309/8010-8012/91483-91488 (21 ids total).</summary>
    private static readonly HashSet<int> CustomDecoStatItemExplicitIds =
    [
        594, 595, 596,
        1385, 1389, 1393,
        1483, 1484, 1485,
        2307, 2308, 2309,
        8010, 8011, 8012,
        91483, 91484, 91485, 91486, 91487, 91488
    ];

    // ---- GetBaseVitality/Strength/Ki(Intelligence)/Wisdom(Dexterity) ----

    /// <summary>
    ///     stat = aHalo + rawStat + sum(13 slots) item stat + titleBonus + customDecoBonus. The aHalo term is
    ///     not a typo -- it's added here on top of CriticalDefence's own separate halo/10 bonus; both are
    ///     real, independent uses of the same field.
    /// </summary>
    // The four base primary-stat getters take cosmetic (rune + costume), consumable and mount (absorbed-animal
    // add) contexts as a B1 plumbing seam. WORKSTREAM B5 made the rune portion of the cosmetic context LIVE:
    // each getter now adds its rune-socket contribution via Rune{Vitality,Strength,Ki,Wisdom}Bonus below
    // (decoded from CosmeticContext.RuneStatValues, MyFactor.cpp section 4). WORKSTREAM B6 (Wave-6) made the
    // costume half of cosmetic LIVE too: each getter adds Costume{Vitality,Strength,Ki,Wisdom}Contribution
    // (the cached aCostumeValue block plus the costume-enchant cs, see StatCalculator.CostumeContribution.cs);
    // Ki reads the costume's Int and Wisdom reads the costume's Dex, mirroring the rune terms' own naming
    // inversion. WORKSTREAM B8-mount (Wave-6) made the Tier-2b absorb half of mount LIVE too: each getter adds
    // MountAbsorbPrimaryBonus (0 in production until the MyAnimal base table lands -- see
    // StatCalculator.MountGradeContribution.cs). The remaining cosmetic field (stellar) stays an inert seam
    // here (stellar has no base-primary-stat term in the cited legacy). WORKSTREAM B2 DECISION -- the consumable
    // elixir counters are DELIBERATELY NOT read here: the source finding paraphrased the strength/dexterity
    // elixirs as feeding "base attributes", but the cited legacy (MyFactor::GetZoneForElixir) folds the
    // strength elixir into ATTACK POWER (MyFactor.cpp:657,662, ComputeAttackPower) and the dexterity elixir
    // into ACCURACY and BLOCK (MyFactor.cpp:694,699 / :726,731, ComputeAttackSuccess/ComputeAttackBlock) --
    // never into base Strength/Dexterity (a repo-wide read finds no legacy path applying these counters to a
    // base attribute). So the elixir feed lives in those derived-stat getters, not here. The consumable
    // parameter stays on these getters as a still-inert seam; dropping it would touch the ComputeBaseStats
    // call site for no behavioral gain.
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
        total += CostumeVitalityContribution(cosmetic.CostumeValue, cosmetic.CostumeEnchantCs); // B6 costume
        total += MountAbsorbPrimaryBonus(mount); // B8 mount Tier 2b absorb

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
        total += CostumeStrengthContribution(cosmetic.CostumeValue, cosmetic.CostumeEnchantCs); // B6 costume
        total += MountAbsorbPrimaryBonus(mount); // B8 mount Tier 2b absorb

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
            cosmetic.CostumeEnchantCs); // B6 costume (reads costume Int)
        total += MountAbsorbPrimaryBonus(mount); // B8 mount Tier 2b absorb

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
            cosmetic.CostumeEnchantCs); // B6 costume (reads costume Dex)
        total += MountAbsorbPrimaryBonus(mount); // B8 mount Tier 2b absorb

        return total;
    }

    // ---- Rune-system per-socket base-attribute contribution ----
    // Server/Header/Protocol/MyFactor.cpp:1685-1695 (Vitality), :1744-1754 (Ki/Int), :1803-1813 (Strength),
    // :1862-1872 (Dex/Wisdom). Each of the 4 rune sockets can add an independent signed byte to all four base
    // attributes at once, decoded from that socket's packed aRuneSystemStat via RuneStatDecoder. A socket
    // contributes only when its item id is > 0 (occupied) AND the decoded byte is strictly > 0 -- a stored byte
    // of 0, or of 128-255 (the latter read back negative, see RuneStatDecoder), adds nothing. Additive on top
    // of the points/equipment/title/deco sums above; order-independent (pure int addition), so appended to each
    // getter's return. CosmeticContext's rune arrays default to an uninitialised ImmutableArray (a `default`
    // CosmeticContext, i.e. the no-runtime-state path), hence the IsDefaultOrEmpty guard.

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

    /// <summary>
    ///     Decoration slots are the raw equipment-array indices 9-12 (EDECO1-EDECO4, STRUCT.h:1662-1676).
    ///     Each qualifying item whose own raw stat is below 100 contributes (100 - rawStat); an empty slot or
    ///     a non-qualifying item silently contributes 0.
    /// </summary>
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

    // ---- Title-rank bonus tranches (deliberately non-uniform if-nesting per stat) ----

    private static int TitleRankBonus(int[] table, int rank)
    {
        return rank is >= 1 and <= 14 ? table[rank - 1] : 0;
    }

    private static int TitleVitalityBonus(int title)
    {
        if (title <= 0) return 0;
        var rank = title % 100;
        // No <=200 test (101-300 both map to B) -- confirmed gap, not an omission.
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
        // No <=400 test (301-400 and >400 both map to D) -- confirmed gap, not an omission.
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
