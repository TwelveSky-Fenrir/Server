using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Loot;

public readonly record struct DroppedItem(int ItemId, int Quantity, int Serial = 0);

public sealed record MonsterDropResult(long? Money, IReadOnlyList<DroppedItem> Items);

public sealed class MonsterDropRoller(
    WorldDataCache worldData,
    Random random,
    float itemDropRatio = MonsterDropRoller.DefaultItemDropRatio,
    float rareDropRatio = MonsterDropRoller.DefaultRareDropRatio,
    float userDropRatio = MonsterDropRoller.DefaultUserDropRatio)
{

        public const float DefaultItemDropRatio = 20.0f;

        public const float DefaultRareDropRatio = 20.0f;

        public const float DefaultUserDropRatio = 1.0f;

        private const int UnconditionalItem864Id = 864;

    private const int UnconditionalItem864Threshold = 1000;

        private const float PremiumDropRatioBonus = 1.0f;

        public static bool IsEligible(MonsterRowDto monster, short killerLevel)
    {
        if (monster.MartialItemLevel >= 1)
            return false;

        return killerLevel - monster.ItemLevel <= 9;
    }

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
                    itemType = 1;
                    temp = (int)((rate.Value + killerLuck) * commonUniqueRatio);
                    break;
                case < 6:
                    itemType = 2;
                    temp = (int)((rate.Value + killerLuck) * commonUniqueRatio);
                    break;
                default:
                    itemType = 3;
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

        private void RollQuestItem(MonsterDropQuestItemRowDto? dropQuestItem, QuestProgress killerQuest,
        Func<int, bool> killerHasItem, List<DroppedItem> items)
    {
        if (dropQuestItem is not { DropRate: > 0 })
            return;

        if (LootRandomSource.RandomNumber(random) > dropQuestItem.DropRate)
            return;

        if (killerQuest.ActiveFlag != 1 || killerQuest.QSort != 2 ||
            killerQuest.TargetPhase != dropQuestItem.QuestItemId)
            return;

        if (!killerHasItem(dropQuestItem.QuestItemId))
            items.Add(new DroppedItem(dropQuestItem.QuestItemId, 1));
    }

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

        private static bool IsLnw33ExtraDropBlacklisted(int itemId)
    {
        return itemId is 1072 or 1073 or 1074 or 695 or 864 or 1048;
    }
}
