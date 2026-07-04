using Fenrir.Application.Game.GameData;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.World.Loot;

/// <summary>
///     Ports <c>ITEMSYSTEM::ReturnDropRareItem</c> + its <c>Return(level,type,sort)</c> helper (verified
///     against source, <c>Server/ts25zone/GameSystem/GameSystem_02_Item.cpp:299-1023</c>): picks ONE random
///     equipment item matching a rolled (Level, Type, Sort) triple, out of the whole <c>world.Items</c>
///     catalog, subject to the same eligibility gates the legacy checks. Used only by the "general item"
///     drop-category tier (report ServerDocs/30_Fenrir_ServerLogic/05_game_mechanics.md §5, the
///     <c>DROP_GENERAL_ITEM</c> block) -- money/potion/quest/extra items are plain id lookups, not this
///     random-catalog-search.
/// </summary>
/// <remarks>
///     Legacy indexes items via a prebuilt <c>mPART[level][type][sort]</c> bucket for O(1) random pick;
///     Fenrir has no such prebuilt index (out of this pass's scope -- <see cref="WorldDataCache.ItemsById" />
///     is keyed by ItemId, not by (Level,Type,Sort)), so this filters <see cref="WorldDataCache.ItemsById" />
///     linearly per attempt instead. Monster deaths are not a per-network-frame hot path (report 05 §0: the
///     whole legacy simulation is 2 Hz), so a linear scan over the item catalog (a few thousand rows) per
///     roll is an acceptable, documented simplification -- a precomputed (Level,Type,Sort) index would be the
///     natural follow-up if profiling ever shows otherwise.
///     <para>
///         The level==145 (<c>MAX_LIMIT_LEVEL_NUM</c>) "high level"/rebirth item bucket (legacy's own special
///         <c>iMartialLevel</c>-keyed search past that boundary) is NOT ported -- Fenrir has no rebirth/high-level
///         item system modeled yet; a roll that lands on level 145 is resolved with the SAME plain
///         Level-equality filter as any other level, which under-serves that one boundary level but invents
///         nothing (open issue, not silently guessed).
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
        Pet = 22; // ports the legacy's "iSort >= IPET" reject -- never actually reachable via the pool below, kept for parity

    private const int MaxAttempts = 10;

    /// <summary>
    ///     <paramref name="killerTribe" /> stands in for the legacy's <c>tPreviousTribe</c> (report 05 §4/§12:
    ///     "aPreviousTribe", the tribe a character belonged to before its last faction change) -- Fenrir does
    ///     not track faction-change history, so a character's CURRENT tribe is used, which is exact for any
    ///     character that has never changed faction (the common case) and a documented approximation
    ///     otherwise (open issue).
    /// </summary>
    public static int? Resolve(WorldDataCache worldData, Random random, byte killerTribe, int itemType,
        int levelLow, int levelHigh)
    {
        Span<int> sortPool =
        [
            Amulet, Armor, Glove, Ring, Boots,
            killerTribe switch
            {
                0 => Sword,
                1 => Katana,
                2 => LongBlade,
                _ => -1
            },
            killerTribe switch
            {
                0 => Blade,
                1 => DoubleBlade,
                2 => Spear,
                _ => -1
            },
            killerTribe switch
            {
                0 => Marble,
                1 => Lute,
                2 => Scepter,
                _ => -1
            },
            Cape,
            SkillBook
        ];

        if (sortPool[5] < 0)
            return
                null; // unknown tribe -- legacy's own ReturnDropRareItem returns NULL for tPreviousTribe outside 0..2

        var chosenSort = sortPool[random.Next(sortPool.Length)];

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
    ///     catalog (see class remarks on why this is a linear scan here).
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
