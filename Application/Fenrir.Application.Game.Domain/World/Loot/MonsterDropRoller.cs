using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>One resolved, ready-to-spawn drop item (never money -- see <see cref="MonsterDropResult.Money" />).</summary>
public readonly record struct DroppedItem(int ItemId, int Quantity);

/// <summary>
///     Everything one monster's death rolled, in pipeline order: money, potions, general items, quest item, extra
///     items, item 864.
/// </summary>
public sealed record MonsterDropResult(long? Money, IReadOnlyList<DroppedItem> Items);

/// <summary>
///     Generic monster death drop pipeline (<c>Server/ts25zone/S07_MyGame05.cpp:2664-2999</c>): money -&gt;
///     potions -&gt; general items (rare-table search) -&gt; quest item -&gt; extra items -&gt; unconditional
///     item-864 roll. The quest-item roll (<c>DROP_QUEST_ITEM</c>) is the one tier NOT gated by
///     <see cref="IsEligible" /> in the source -- see <see cref="RollQuestItem" />.
/// </summary>
/// <remarks>
///     <para>
///         Immediately BEFORE this pipeline (<c>Server/ts25zone/S07_MyGame05.cpp:2333-2662</c>, the section
///         directly preceding <c>DROP_MONEY</c>) sits a separate, hardcoded per-monster-id boss/event drop
///         tier -- <see cref="BossEventDropResolver" />, called by <c>MonsterSpawnScheduler.ProcessDeath</c>
///         before it ever reaches this class. Only one block in that range (<c>mIndex==1400||1406</c>, "BOSS
///         POPUP", <c>:2256-2331</c>) is genuinely dead/commented-out; the ~8 identifier matches
///         <see cref="BossEventDropResolver" /> ports are live, unguarded <c>if</c> statements that run on
///         every <c>ReleaseEU33</c> build -- this class's own doc comment previously (incorrectly) claimed the
///         whole ~15-block range was dead code. Several of those identifiers <c>return</c> before ever
///         reaching this class's own <see cref="Roll" />, which is why <c>MonsterSpawnScheduler.ProcessDeath</c>
///         may skip calling <see cref="Roll" /> entirely for a given kill -- see
///         <see cref="BossEventDropResolver" />'s own remarks for exactly which identifiers do and don't.
///     </para>
///     <para>
///         Immediately AFTER this pipeline (<c>Server/ts25zone/S07_MyGame05.cpp:3000-3732</c>, the remainder
///         of <c>ProcessForDropItem</c>) sits a further two live drop tiers -- <see cref="MonsterDropTailResolver" />,
///         also called by <c>MonsterSpawnScheduler.ProcessDeath</c>, immediately after this class's own
///         <see cref="Roll" />. Everything else in that tail span is dead code (commented out or gated by a
///         macro never <c>#define</c>'d anywhere in <c>Server/</c>) -- see that class's own remarks.
///     </para>
///     <para>
///         <c>item_drop</c>/<c>rare_drop</c> default 20.0, from <c>ServerInfo.ini</c>'s <c>ItemDropUpRatio=200</c>
///         via <c>CreateRatio0(x) = x * 0.1f</c>. <c>user_drop</c> defaults 1.0; the zone-120 newbie bonus is not
///         modeled -- constructor params exist so callers can supply the real values once that system exists. The
///         per-tribe <c>mTribeItemDropUpRatioInfo</c>/<c>mTribeItemDropUpRatioForMyoungInfo</c> modifiers
///         (<c>S07_MyGame05.cpp:2713-2714</c>) are not applied here either -- <c>WorldInfo.TribeItemDropUpRatioInfo</c>/
///         <c>TribeItemDropUpRatioForMyoungInfo</c> have no persisted source yet (<c>WorldStateTemplates</c> zeroes
///         both), so there is nothing live to read.
///     </para>
///     <para>
///         The premium-account +1.0 bonus to all three of <c>user_drop</c>/<c>item_drop</c>/<c>rare_drop</c>
///         (behavior contract "Premium-account drop-rate bonus on monster kill") IS modeled -- see
///         <see cref="Roll" />'s own <paramref name="killerPremiumActive" /> parameter, applied locally per kill
///         rather than baked into these constructor-level ratios, since those are shared across every kill a
///         zone's single <see cref="MonsterDropRoller" /> instance ever rolls while premium status is per-killer.
///         Réf. C++ : <c>Server/ts25zone/S07_MyGame05.cpp:2171-2176</c> (the premium gate itself, checked as a
///         plain greater-than-zero comparison against the stored <c>aPremium</c> expiry timestamp, never
///         re-validated against the current time at this call site -- a separate per-minute maintenance pass,
///         mirrored here by <see cref="Simulation.SupportSkillTimeUpRatioMaintenanceSystem" />, is what actually
///         zeroes an expired value) ; <c>:2101-2124</c> (the preceding non-premium setup of the same three rates)
///         ; <c>Server/ts25zone/S07_MyGame01.cpp:442-444</c> and <c>Server/Header/function.h:1654-1656</c>
///         (<c>CreateRatio0</c> deriving the 20.0 defaults) ; <c>Server/BuildEU33/ServerInfo.ini:164-168</c>
///         (confirming the production config values feeding those defaults).
///     </para>
/// </remarks>
public sealed class MonsterDropRoller(
    WorldDataCache worldData,
    Random random,
    float itemDropRatio = MonsterDropRoller.DefaultItemDropRatio,
    float rareDropRatio = MonsterDropRoller.DefaultRareDropRatio,
    float userDropRatio = MonsterDropRoller.DefaultUserDropRatio)
{
    /// <summary>See class remarks.</summary>
    public const float DefaultItemDropRatio = 20.0f;

    /// <summary>See class remarks.</summary>
    public const float DefaultRareDropRatio = 20.0f;

    /// <summary>See class remarks.</summary>
    public const float DefaultUserDropRatio = 1.0f;

    /// <summary>Item 864 unconditional roll threshold -- gated by <see cref="IsEligible" />, same as the rest of this method.</summary>
    private const int UnconditionalItem864Id = 864;

    private const int UnconditionalItem864Threshold = 1000;

    /// <summary>
    ///     Flat bonus <see cref="Roll" /> adds to each of <c>user_drop</c>/<c>item_drop</c>/<c>rare_drop</c> for
    ///     the duration of one kill's roll when <paramref name="killerPremiumActive" /> is true. See class
    ///     remarks for the full citation chain.
    /// </summary>
    private const float PremiumDropRatioBonus = 1.0f;

    /// <summary>
    ///     Ports <c>tCheckPossibleDrop</c>'s normal branch: eligible only when the killer is at most 9 levels
    ///     above the monster's <c>ItemLevel</c>.
    /// </summary>
    /// <remarks>
    ///     The legacy's other branch (gate on post-cap "Level2") is not ported -- no such field exists yet, so
    ///     those monsters never drop loot here.
    /// </remarks>
    public static bool IsEligible(MonsterRowDto monster, short killerLevel)
    {
        if (monster.MartialItemLevel >= 1)
            return false;

        return killerLevel - monster.ItemLevel <= 9;
    }

    /// <param name="killerLuck">
    ///     Pre-scaled like legacy's <c>tMasterLuck = luck * 10</c> -- pass <c>killer.Stats.Luck * 10</c>, not
    ///     the raw <see cref="Stats.EffectiveStats.Luck" /> value.
    /// </param>
    /// <param name="killerQuest">The killer's live quest state, for <see cref="RollQuestItem" />.</param>
    /// <param name="killerHasItem">Killer inventory lookup, for <see cref="RollQuestItem" />.</param>
    /// <param name="killerPremiumActive">
    ///     Whether the killer's premium status is currently active -- see class remarks for the full citation
    ///     chain. Callers should pass a plain greater-than-zero check on the killer's stored premium-expiry
    ///     value (e.g. <c>PlayerRuntimeState.PremiumExpireUtc &gt; 0</c>), not a re-comparison against the
    ///     current time: that expiry check belongs to a separate periodic maintenance pass, not this call site.
    /// </param>
    public MonsterDropResult Roll(MonsterDefinition monster, short killerLevel, byte killerTribe, int killerLuck,
        QuestProgress killerQuest, Func<int, bool> killerHasItem, bool killerPremiumActive = false)
    {
        var eligible = IsEligible(monster.Monster, killerLevel);

        var premiumBonus = killerPremiumActive ? PremiumDropRatioBonus : 0f;
        var effectiveItemDropRatio = itemDropRatio + premiumBonus;
        var effectiveRareDropRatio = rareDropRatio + premiumBonus;
        var effectiveUserDropRatio = userDropRatio + premiumBonus;

        var money = eligible ? RollMoney(monster.DropMoney, killerLuck, effectiveItemDropRatio) : null;
        var items = new List<DroppedItem>();

        if (eligible)
        {
            RollPotions(monster.DropPotions, killerLuck, effectiveItemDropRatio, items);
            RollGeneralItems(monster.Monster, monster.DropCategoryRates, killerTribe, killerLuck,
                effectiveUserDropRatio, effectiveItemDropRatio, effectiveRareDropRatio, items);
        }

        RollQuestItem(monster.DropQuestItem, killerQuest, killerHasItem, items);

        if (!eligible) return new MonsterDropResult(money, items);
        RollExtraItems(monster.DropExtraItems, items);

        if (LootRandomSource.RandomNumber(random) <= UnconditionalItem864Threshold)
            items.Add(new DroppedItem(UnconditionalItem864Id, 1));

        return new MonsterDropResult(money, items);
    }

    /// <summary>Ports <c>DROP_MONEY</c> with the LNW33 adjustment active: a roll over 500 loses 30%, then gains a flat +2000.</summary>
    private long? RollMoney(MonsterDropMoneyRowDto? dropMoney, int killerLuck, float effectiveItemDropRatio)
    {
        if (dropMoney is not { DropRate: > 0 })
            return null;

        if (LootRandomSource.RandomNumber(random) > (int)((dropMoney.DropRate + killerLuck) * effectiveItemDropRatio))
            return null;

        var size = dropMoney.MinAmount + random.Next(dropMoney.MaxAmount - dropMoney.MinAmount + 1);

        if (size > 500)
            size -= (int)(size * 0.3f);
        size += 2000;

        return size > 0 ? size : null;
    }

    /// <summary>Ports <c>DROP_POTION</c> -- 5 independent slots, each item id always drops quantity 1.</summary>
    private void RollPotions(IReadOnlyList<MonsterDropPotionRowDto> potions, int killerLuck,
        float effectiveItemDropRatio, List<DroppedItem> items)
    {
        foreach (var potion in potions)
        {
            if (potion.DropRate <= 0)
                continue;

            if (LootRandomSource.RandomNumber(random) <= (int)((potion.DropRate + killerLuck) * effectiveItemDropRatio))
                items.Add(new DroppedItem(potion.PotionItemId, 1));
        }
    }

    /// <summary>
    ///     Ports <c>DROP_GENERAL_ITEM</c>: 12 category-rate slots, grouped Common(0-2)/Unique(3-5)/Rare(6-8)/Elite(9-11).
    ///     Elite is dead code in the source, so slots 9-11 are never rolled here either.
    /// </summary>
    private void RollGeneralItems(MonsterRowDto monster, IReadOnlyList<MonsterDropCategoryRateRowDto> rates,
        byte killerTribe, int killerLuck, float effectiveUserDropRatio, float effectiveItemDropRatio,
        float effectiveRareDropRatio, List<DroppedItem> items)
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

        // itDropCommon/itDropUnique use item_drop; itDropRare uses rare_drop instead.
        var commonUniqueRatio = effectiveUserDropRatio + effectiveItemDropRatio;
        var rareRatio = effectiveUserDropRatio + effectiveRareDropRatio;

        foreach (var rate in rates)
        {
            if (rate.Value <= 0 || rate.CategoryIndex >= 9)
                continue;

            int itemType;
            int temp;
            switch (rate.CategoryIndex)
            {
                case < 3:
                    itemType = 1; // ICOMMON
                    temp = (int)((rate.Value + killerLuck) * commonUniqueRatio);
                    break;
                case < 6:
                    itemType = 2; // IUNIQUE
                    temp = (int)((rate.Value + killerLuck) * commonUniqueRatio);
                    break;
                default:
                    itemType = 3; // IRARE, no luck term (matches source)
                    temp = (int)(rate.Value * rareRatio);
                    break;
            }

            if (LootRandomSource.RandomNumber(random) > temp)
                continue;

            var itemId = GeneralItemDropResolver.Resolve(worldData, random, killerTribe, itemType, levelLow, levelHigh);
            if (itemId is { } resolved)
                items.Add(new DroppedItem(resolved, 1));
        }
    }

    /// <summary>
    ///     Ports <c>DROP_QUEST_ITEM</c> (<c>S07_MyGame05.cpp:2875-2884</c>): gate roll first (raw rate, no
    ///     luck/ratio term, same as <see cref="RollExtraItems" />), then drop only when the killer's active
    ///     quest is qSort 2 (item acquisition) targeting exactly this item and they don't already hold it --
    ///     <c>ReturnQuestPresentState() == 2</c> for qSort 2 means "in progress," i.e. item not yet held.
    /// </summary>
    private void RollQuestItem(MonsterDropQuestItemRowDto? dropQuestItem, QuestProgress killerQuest,
        Func<int, bool> killerHasItem, List<DroppedItem> items)
    {
        if (dropQuestItem is not { DropRate: > 0 })
            return;

        if (LootRandomSource.RandomNumber(random) > dropQuestItem.DropRate)
            return;

        // ActiveFlag==1 is implied by QSort==2 (Accept/Complete/Abandon always set/clear them together) but
        // checked explicitly to mirror ReturnQuestPresentState's own idle guard.
        if (killerQuest.ActiveFlag != 1 || killerQuest.QSort != 2 ||
            killerQuest.TargetPhase != dropQuestItem.QuestItemId)
            return;

        if (!killerHasItem(dropQuestItem.QuestItemId))
            items.Add(new DroppedItem(dropQuestItem.QuestItemId, 1));
    }

    /// <summary>
    ///     Ports <c>DROP_EXTRA_ITEM</c>: 50 slots, raw rate (no luck/ratio multiplier), gated by
    ///     <c>iCheckMonsterDrop == 2</c> and the LNW33 blacklist below.
    /// </summary>
    private void RollExtraItems(IReadOnlyList<MonsterDropExtraItemRowDto> extraItems, List<DroppedItem> items)
    {
        foreach (var extra in extraItems)
        {
            if (extra.DropRate <= 0 || extra.ItemId is not { } itemId)
                continue;

            if (LootRandomSource.RandomNumber(random) > extra.DropRate)
                continue;

            if (!worldData.ItemsById.TryGetValue(itemId, out var definition) || definition.Item.CheckMonsterDrop != 2)
                continue;

            if (IsLnw33ExtraDropBlacklisted(itemId))
                continue;

            items.Add(new DroppedItem(itemId, 1));
        }
    }

    /// <summary>
    ///     LNW33's <c>IGNORE_DROP_EXTRA</c> switch: these 6 item ids are excluded from the extra-item tier even
    ///     when otherwise eligible. Every other case in that switch is dead code, so this is the whole list.
    /// </summary>
    private static bool IsLnw33ExtraDropBlacklisted(int itemId)
    {
        return itemId is 1072 or 1073 or 1074 or 695 or 864 or 1048;
    }
}
