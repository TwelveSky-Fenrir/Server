using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Ports <c>ITEMSYSTEM::ReturnDropRareItem</c> + its <c>Return(level,type,sort)</c> helper
///     (<c>Server/ts25zone/GameSystem/GameSystem_02_Item.cpp:299-1023</c>): picks one random equipment item
///     matching a rolled (Level, Type, Sort) triple out of the whole <c>world.Items</c> catalog. Used only by
///     the "general item" drop-category tier -- money/potion/quest/extra items are plain id lookups, not this
///     random-catalog-search.
/// </summary>
/// <remarks>
///     Legacy indexes items via a prebuilt <c>mPART[level][type][sort]</c> bucket for O(1) random pick;
///     Fenrir has no such prebuilt index, so this filters <see cref="WorldDataCache.ItemsById" /> linearly per
///     attempt instead -- acceptable since monster deaths are not a per-network-frame hot path.
///     <para>
///         The level==145 (<c>MAX_LIMIT_LEVEL_NUM</c>) high-level/rebirth item bucket is not ported -- Fenrir
///         has no rebirth/high-level item system yet, so a roll landing on level 145 uses the same plain
///         level-equality filter as any other level.
///     </para>
/// </remarks>
public static class GeneralItemDropResolver
{
    private const int Amulet = 7;
    private const int Cape = 8;
    private const int Armor = 9;
    private const int Glove = 10;
    private const int Ring = 11;
    private const int Boots = 12;
    private const int Sword = 13;
    private const int Blade = 14;
    private const int Marble = 15;
    private const int Katana = 16;
    private const int DoubleBlade = 17;
    private const int Lute = 18;
    private const int LongBlade = 19;
    private const int Spear = 20;
    private const int Scepter = 21;
    private const int SkillBook = 5;

    private const int
        Pet = 22; // ports the legacy's "iSort >= IPET" reject -- unreachable via the pool below, kept for parity

    private const int MaxAttempts = 10;

    /// <summary>
    ///     <paramref name="killerTribe" /> stands in for the legacy's <c>tPreviousTribe</c> (the tribe a
    ///     character belonged to before its last faction change) -- Fenrir does not track faction-change
    ///     history, so the character's current tribe is used instead, exact unless the character has
    ///     transferred factions.
    /// </summary>
    /// <param name="includeCape">
    ///     Whether the pool includes the Cape sort at all (<c>ReturnDropRareItem</c>'s <c>iAddSort</c>
    ///     parameter) -- the general monster-item-drop call site (<c>S07_MyGame05.cpp:2794</c>) passes this
    ///     unconditionally true; the Lucky Ticket call site (<c>S04_MyWork03.cpp:3461-3467</c>) passes it only
    ///     when the resolved tier is IRARE. Defaults to <see langword="true" /> so every pre-existing caller
    ///     keeps its prior behavior unchanged.
    /// </param>
    /// <param name="includeSkillBook">
    ///     Whether the pool includes the Skill Book sort at all (<c>ReturnDropRareItem</c>'s <c>iAddSort2</c>
    ///     parameter) -- the general monster-item-drop call site passes this unconditionally true; the Lucky
    ///     Ticket call site never sets it (always 0/false). Defaults to <see langword="true" /> for the same
    ///     back-compat reason as <paramref name="includeCape" />.
    /// </param>
    public static int? Resolve(WorldDataCache worldData, Random random, byte killerTribe, int itemType,
        int levelLow, int levelHigh, bool includeCape = true, bool includeSkillBook = true)
    {
        var weapon1 = killerTribe switch
        {
            0 => Sword,
            1 => Katana,
            2 => LongBlade,
            _ => -1
        };

        if (weapon1 < 0)
            return null; // unknown tribe -- legacy returns null for tPreviousTribe outside 0..2

        var weapon2 = killerTribe switch
        {
            0 => Blade,
            1 => DoubleBlade,
            2 => Spear,
            _ => -1
        };

        var weapon3 = killerTribe switch
        {
            0 => Marble,
            1 => Lute,
            2 => Scepter,
            _ => -1
        };

        // tSortSearch in the legacy source: 8 fixed slots, +1 for a live iAddSort, +1 for a live iAddSort2.
        Span<int> pool = stackalloc int[10];
        var poolSize = 0;
        pool[poolSize++] = Amulet;
        pool[poolSize++] = Armor;
        pool[poolSize++] = Glove;
        pool[poolSize++] = Ring;
        pool[poolSize++] = Boots;
        pool[poolSize++] = weapon1;
        pool[poolSize++] = weapon2;
        pool[poolSize++] = weapon3;
        if (includeCape)
            pool[poolSize++] = Cape;
        if (includeSkillBook)
            pool[poolSize++] = SkillBook;

        var chosenSort = pool[random.Next(poolSize)];

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var level = levelLow == levelHigh ? levelLow : levelLow + random.Next(levelHigh - levelLow + 1);

            var candidate = ReturnOne(worldData, random, level, itemType, chosenSort);
            if (candidate is null)
                continue;

            if (candidate.CheckMonsterDrop != 2 || candidate.CheckAvatarTrade != 2 || candidate.CheckSetItem != 1 ||
                candidate.Sort >= Pet)
                continue;

            // iEquipInfo[0] == 1 (usable by every tribe) OR iEquipInfo[0] - 2 == tribe (tribe-restricted: 2/3/4 -> tribe 0/1/2).
            if (candidate.EquipInfo1 == 1 || candidate.EquipInfo1 - 2 == killerTribe)
                return candidate.ItemId;
        }

        return null;
    }

    /// <summary>
    ///     Ports <c>ITEMSYSTEM::Return(level,type,sort)</c>'s "one uniformly random match" semantics over the whole
    ///     catalog.
    /// </summary>
    private static ItemRowDto? ReturnOne(WorldDataCache worldData, Random random, int level, int type, int sort)
    {
        List<ItemRowDto>? matches = null;
        foreach (var definition in worldData.ItemsById.Values)
        {
            var item = definition.Item;
            if (item.Level != level || item.Type != type || item.Sort != sort)
                continue;

            (matches ??= []).Add(item);
        }

        return matches is null or { Count: 0 } ? null : matches[random.Next(matches.Count)];
    }
}
