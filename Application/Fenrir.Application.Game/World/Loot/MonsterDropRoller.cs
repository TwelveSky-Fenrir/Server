using Fenrir.Application.Game.GameData;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.World.Loot;

/// <summary>One resolved, ready-to-spawn drop item (never money -- see <see cref="MonsterDropResult.Money" />).</summary>
public readonly record struct DroppedItem(int ItemId, int Quantity);

/// <summary>Everything one monster's death rolled, in pipeline order (report 05 §5): money, potions, general items, extra items, the unconditional item 864.</summary>
public sealed record MonsterDropResult(long? Money, IReadOnlyList<DroppedItem> Items);

/// <summary>
///     The generic monster death drop pipeline (report
///     ServerDocs/30_Fenrir_ServerLogic/05_game_mechanics.md §5, verified against
///     <c>Server/ts25zone/S07_MyGame05.cpp:2664-2999</c>): money -&gt; potions -&gt; general items (rare-table
///     search) -&gt; extra items -&gt; the unconditional item-864 roll. Quest-item drops (<c>DROP_QUEST_ITEM</c>)
///     are DELIBERATELY NOT rolled here -- see this task's StructuredOutput openIssues (gating on the
///     killer's live quest progress requires a quest-state system that does not exist in Fenrir yet; rolling
///     the item unconditionally would be a fabricated, spammy divergence, not a faithful port).
///     ~15 event-specific/boss-ID-specific blocks (Santa 731, Yanggok bosses 564-568, Demon Lord 287's static
///     kill counter, item 746/9001 special pools, etc.) are likewise NOT ported -- report 05 §5 itself
///     documents almost all of them as behind local <c>#define</c>s that are commented OUT in this reference
///     build, i.e. already dead code in the source being ported.
/// </summary>
/// <remarks>
///     <c>item_drop</c>/<c>rare_drop</c> default to 20.0 -- verified from the reference build's own
///     <c>Server/BuildEU33/ServerInfo.ini</c> (<c>ItemDropUpRatio=200</c>, <c>ItemDropUpRatioForRare=200</c>)
///     run through <c>CreateRatio0(x) = x * 0.1f</c> (<c>Server/Header/function.h:1654-1657</c>).
///     <c>user_drop</c> defaults to 1.0 (the per-player field's own default, <c>S04_MyWork02.cpp:527</c>; it
///     only rises to 1.1 while standing in zone 120, a single newbie-zone bonus this pass does not model) and
///     the "premium account" +1.0 bonus to all three ratios (<c>aPremium &gt; 0</c>) is also not modeled --
///     Fenrir has no premium-account flag yet. All three are constructor parameters specifically so a caller
///     CAN supply the real values the day those systems exist, rather than baking the gap in silently.
/// </remarks>
public sealed class MonsterDropRoller(
    WorldDataCache worldData,
    Random random,
    float itemDropRatio = MonsterDropRoller.DefaultItemDropRatio,
    float rareDropRatio = MonsterDropRoller.DefaultRareDropRatio,
    float userDropRatio = MonsterDropRoller.DefaultUserDropRatio)
{
    /// <summary>Verified from Server/BuildEU33/ServerInfo.ini's ItemDropUpRatio=200 via CreateRatio0 (x*0.1f) -- see class remarks.</summary>
    public const float DefaultItemDropRatio = 20.0f;

    /// <summary>Verified from Server/BuildEU33/ServerInfo.ini's ItemDropUpRatioForRare=200 via CreateRatio0 -- see class remarks.</summary>
    public const float DefaultRareDropRatio = 20.0f;

    /// <summary>tUserInfo-&gt;mItemDropUpRatio's own default (S04_MyWork02.cpp:527) -- see class remarks.</summary>
    public const float DefaultUserDropRatio = 1.0f;

    /// <summary>Item 864 ("Fist Scroll Box") unconditional roll threshold -- <c>RandomNumber() &lt;= 1000</c> (S07_MyGame05.cpp:2995-2998), NOT gated by <see cref="IsEligible" /> in the source... actually it IS: both live inside the same <c>if (tCheckPossibleDrop)</c> block.</summary>
    private const int UnconditionalItem864Id = 864;

    private const int UnconditionalItem864Threshold = 1000;

    /// <summary>
    ///     Ports <c>tCheckPossibleDrop</c>'s "normal" branch (<c>shmMONSTER_INFO-&gt;mMartialItemLevel &lt; 1</c>,
    ///     S07_MyGame05.cpp:2202-2210): eligible only when the killer is at most 9 levels above the monster's
    ///     <c>ItemLevel</c>.
    /// </summary>
    /// <remarks>
    ///     The legacy's OTHER branch (<c>mMartialItemLevel &gt;= 1</c>: gate on the killer's post-cap "Level2"
    ///     instead) is not ported -- <see cref="World.PlayerRuntimeState" /> has no "Level2"/rebirth-tier level
    ///     field yet (a different, not-yet-built system). Such monsters (high-level/rebirth-tier templates)
    ///     conservatively never drop loot in this pass rather than guessing a formula -- open issue, not a
    ///     fabricated value.
    /// </remarks>
    public static bool IsEligible(MonsterRowDto monster, short killerLevel)
    {
        if (monster.MartialItemLevel >= 1)
            return false;

        return killerLevel - monster.ItemLevel <= 9;
    }

    /// <param name="killerLuck">
    ///     Pre-scaled exactly like the legacy's own <c>tMasterLuck = tUserInfo-&gt;mFactor.GetLuck() * 10</c>
    ///     (S07_MyGame05.cpp:2190) -- callers must pass <c>killer.Stats.Luck * 10</c>, not the raw
    ///     <see cref="Stats.EffectiveStats.Luck" /> value.
    /// </param>
    public MonsterDropResult Roll(MonsterDefinition monster, short killerLevel, byte killerTribe, int killerLuck)
    {
        var eligible = IsEligible(monster.Monster, killerLevel);

        var money = eligible ? RollMoney(monster.DropMoney, killerLuck) : null;
        var items = new List<DroppedItem>();

        if (eligible)
        {
            RollPotions(monster.DropPotions, killerLuck, items);
            RollGeneralItems(monster.Monster, monster.DropCategoryRates, killerTribe, killerLuck, items);
            RollExtraItems(monster.DropExtraItems, items);

            if (LegacyRandom.RandomNumber(random) <= UnconditionalItem864Threshold)
                items.Add(new DroppedItem(UnconditionalItem864Id, 1));
        }

        return new MonsterDropResult(money, items);
    }

    /// <summary>
    ///     Ports <c>DROP_MONEY</c> (S07_MyGame05.cpp:2668-2692) INCLUDING the LNW33 adjustment -- this
    ///     reference build has LNW33 active (report 05's own build header): a roll over 500 loses 30%, then
    ///     gains a flat +2000.
    /// </summary>
    private long? RollMoney(MonsterDropMoneyRowDto? dropMoney, int killerLuck)
    {
        if (dropMoney is not { DropRate: > 0 } money)
            return null;

        if (LegacyRandom.RandomNumber(random) > (int)((money.DropRate + killerLuck) * itemDropRatio))
            return null;

        var size = money.MinAmount + random.Next(money.MaxAmount - money.MinAmount + 1);

        if (size > 500)
            size -= (int)(size * 0.3f);
        size += 2000;

        return size > 0 ? size : null;
    }

    /// <summary>Ports <c>DROP_POTION</c> (S07_MyGame05.cpp:2697-2706) -- 5 independent slots, each item id always drops quantity 1.</summary>
    private void RollPotions(IReadOnlyList<MonsterDropPotionRowDto> potions, int killerLuck, List<DroppedItem> items)
    {
        foreach (var potion in potions)
        {
            if (potion.DropRate <= 0)
                continue;

            if (LegacyRandom.RandomNumber(random) <= (int)((potion.DropRate + killerLuck) * itemDropRatio))
                items.Add(new DroppedItem(potion.PotionItemId, 1));
        }
    }

    /// <summary>
    ///     Ports <c>DROP_GENERAL_ITEM</c> (S07_MyGame05.cpp:2711-2873): 12 category-rate slots, grouped
    ///     Common(0-2)/Unique(3-5)/Rare(6-8)/Elite(9-11) -- Elite is dead code in the source (the branch that
    ///     would set <c>tCheckDropEvent = TRUE</c> for it is entirely commented out), so slots 9-11 are never
    ///     rolled here either, matching the source exactly (not an omission).
    /// </summary>
    private void RollGeneralItems(MonsterRowDto monster, IReadOnlyList<MonsterDropCategoryRateRowDto> rates,
        byte killerTribe, int killerLuck, List<DroppedItem> items)
    {
        int levelLow, levelHigh;
        if (monster.MartialItemLevel < 1)
        {
            levelLow = monster.ItemLevel;
            levelHigh = Math.Min(monster.ItemLevel + 5, 145);
        }
        else
        {
            levelLow = levelHigh = monster.ItemLevel + monster.MartialItemLevel;
        }

        // itDropCommon == itDropUnique == user_drop + item_drop (temp_itemdrop_x2 defaults to 1.0 -- no
        // active "double item drop" timer modeled, see class remarks); itDropRare uses rare_drop instead.
        var commonUniqueRatio = userDropRatio + itemDropRatio;
        var rareRatio = userDropRatio + rareDropRatio;

        foreach (var rate in rates)
        {
            if (rate.Value <= 0 || rate.CategoryIndex >= 9)
                continue; // slots 9-11 (Elite): dead code in the source, see this method's remarks

            int itemType;
            int temp;
            if (rate.CategoryIndex < 3)
            {
                itemType = 1; // ICOMMON
                temp = (int)((rate.Value + killerLuck) * commonUniqueRatio);
            }
            else if (rate.CategoryIndex < 6)
            {
                itemType = 2; // IUNIQUE
                temp = (int)((rate.Value + killerLuck) * commonUniqueRatio);
            }
            else
            {
                itemType = 3; // IRARE -- no luck term added, matching the source exactly
                temp = (int)(rate.Value * rareRatio);
            }

            if (LegacyRandom.RandomNumber(random) > temp)
                continue;

            var itemId = GeneralItemDropResolver.Resolve(worldData, random, killerTribe, itemType, levelLow, levelHigh);
            if (itemId is { } resolved)
                items.Add(new DroppedItem(resolved, 1));
        }
    }

    /// <summary>Ports <c>DROP_EXTRA_ITEM</c> (S07_MyGame05.cpp:2888-2965): 50 slots, RAW rate (no luck/ratio multiplier), gated by <c>iCheckMonsterDrop == 2</c> and the LNW33 blacklist below.</summary>
    private void RollExtraItems(IReadOnlyList<MonsterDropExtraItemRowDto> extraItems, List<DroppedItem> items)
    {
        foreach (var extra in extraItems)
        {
            if (extra.DropRate <= 0 || extra.ItemId is not { } itemId)
                continue;

            if (LegacyRandom.RandomNumber(random) > extra.DropRate)
                continue;

            if (!worldData.ItemsById.TryGetValue(itemId, out var definition) || definition.Item.CheckMonsterDrop != 2)
                continue;

            if (IsLnw33ExtraDropBlacklisted(itemId))
                continue;

            items.Add(new DroppedItem(itemId, 1));
        }
    }

    /// <summary>
    ///     LNW33's <c>IGNORE_DROP_EXTRA</c> switch (S07_MyGame05.cpp:2904-2958): these 6 item ids are
    ///     explicitly EXCLUDED from the extra-item tier even when otherwise eligible (<c>iCheckMonsterDrop == 2</c>)
    ///     -- verified against source; every other <c>case</c> in that switch is commented out (dead) in the
    ///     reference build, so this is the WHOLE blacklist, not a partial list.
    /// </summary>
    private static bool IsLnw33ExtraDropBlacklisted(int itemId)
    {
        return itemId is 1072 or 1073 or 1074 or 695 or 864 or 1048;
    }
}
