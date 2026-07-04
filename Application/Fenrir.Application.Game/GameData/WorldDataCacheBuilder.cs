using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Commerce;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.GameData;

/// <summary>Pure, SQL-free construction of <see cref="WorldDataCache" /> from raw world.* rows -- kept separate from the loader so it's unit-testable on in-memory rows.</summary>
public static class WorldDataCacheBuilder
{
    /// <summary>Throws when a critical dataset (Items, Monsters, Zones, Levels, Skills) is empty -- an unseeded GameServer must not accept a single connection.</summary>
    public static (WorldDataCache Cache, WorldDataFilterStats Stats) Build(WorldDataRows rows)
    {
        EnsureCriticalDatasetNotEmpty(rows.Items.Count, "world.Items");
        EnsureCriticalDatasetNotEmpty(rows.Monsters.Count, "world.Monsters");
        EnsureCriticalDatasetNotEmpty(rows.Zones.Count, "world.Zones");
        EnsureCriticalDatasetNotEmpty(rows.Levels.Count, "world.Levels");
        EnsureCriticalDatasetNotEmpty(rows.Skills.Count, "world.Skills");

        var (zonesByNumber, stats) = BuildZones(
            rows.Zones, rows.ZonePortals, rows.ZoneSpawnPoints, rows.ZoneNpcSpawns, rows.MonsterSpawnRegions);

        var cache = new WorldDataCache
        {
            ItemsById = BuildItems(rows.Items, rows.ItemBonusSkills),
            SkillsById = BuildSkills(rows.Skills, rows.SkillDescriptions, rows.SkillGrades),
            MonstersById = BuildMonsters(rows.Monsters, rows.MonsterDropMoney, rows.MonsterDropPotions,
                rows.MonsterDropExtraItems, rows.MonsterDropCategoryRates, rows.MonsterDropQuestItems),
            NpcsById = BuildNpcs(rows.Npcs, rows.NpcMenuOptions, rows.NpcShopItems, rows.NpcSkillOffers,
                rows.NpcSpeeches, rows.NpcGambleCosts),
            QuestsById = BuildQuests(rows.Quests, rows.QuestRewards, rows.QuestSpeeches),
            LevelsByLevel = rows.Levels.ToFrozenDictionary(static level => level.Level),
            ZonesByNumber = zonesByNumber,
            GemSocketsById = rows.GemSockets.ToFrozenDictionary(static gem => gem.GemSocketId),
            BloodExchangeCatalog = [.. rows.BloodExchangeCatalog],
            EventDefinitions = [.. rows.EventDefinitions],
            ItemMallProductsById =
                rows.ItemMallProducts.ToFrozenDictionary(static product => product.ItemMallProductId),
            RewardBundleItemsByBundleId = BuildRewardBundles(rows.RewardBundles, rows.RewardBundleItems),
            CashCatalog = CashCatalogBuilder.Build(rows.ItemMallProducts),
            CashCatalogVersion = CashCatalogBuilder.ResolveVersion(rows.ItemMallProducts)
        };

        return (cache, stats);
    }

    /// <summary>Zips world.ItemBonusSkills back under its parent item (world.usp_Item_GetAll RS0+RS1).</summary>
    public static FrozenDictionary<int, ItemDefinition> BuildItems(
        IReadOnlyList<ItemRowDto> items,
        IReadOnlyList<ItemBonusSkillRowDto> bonusSkills)
    {
        var bonusSkillsByItem = GroupToLists(bonusSkills, static row => row.ItemId);
        var result = new Dictionary<int, ItemDefinition>(items.Count);

        foreach (var item in items)
            result.Add(item.ItemId, new ItemDefinition(item, TakeGroup(bonusSkillsByItem, item.ItemId)));

        return result.ToFrozenDictionary();
    }

    /// <summary>Zips description lines and the 2 grade rows back under each skill (world.usp_Skill_GetAll RS0-RS2).</summary>
    public static FrozenDictionary<int, SkillDefinition> BuildSkills(
        IReadOnlyList<SkillRowDto> skills,
        IReadOnlyList<SkillDescriptionRowDto> descriptions,
        IReadOnlyList<SkillGradeRowDto> grades)
    {
        var descriptionsBySkill = GroupToLists(descriptions, static row => row.SkillId);
        var gradesBySkill = GroupToLists(grades, static row => row.SkillId);
        var result = new Dictionary<int, SkillDefinition>(skills.Count);

        foreach (var skill in skills)
            result.Add(skill.SkillId, new SkillDefinition(
                skill,
                TakeGroup(descriptionsBySkill, skill.SkillId),
                TakeGroup(gradesBySkill, skill.SkillId)));

        return result.ToFrozenDictionary();
    }

    /// <summary>Money/quest-item are at-most-one-per-monster in the legacy data; a duplicate would be a seed bug, so the plain Add throws rather than silently picking one.</summary>
    public static FrozenDictionary<int, MonsterDefinition> BuildMonsters(
        IReadOnlyList<MonsterRowDto> monsters,
        IReadOnlyList<MonsterDropMoneyRowDto> dropMoney,
        IReadOnlyList<MonsterDropPotionRowDto> dropPotions,
        IReadOnlyList<MonsterDropExtraItemRowDto> dropExtraItems,
        IReadOnlyList<MonsterDropCategoryRateRowDto> dropCategoryRates,
        IReadOnlyList<MonsterDropQuestItemRowDto> dropQuestItems)
    {
        var moneyByMonster = new Dictionary<int, MonsterDropMoneyRowDto>(dropMoney.Count);
        foreach (var row in dropMoney)
            moneyByMonster.Add(row.MonsterId, row);

        var questItemByMonster = new Dictionary<int, MonsterDropQuestItemRowDto>(dropQuestItems.Count);
        foreach (var row in dropQuestItems)
            questItemByMonster.Add(row.MonsterId, row);

        var potionsByMonster = GroupToLists(dropPotions, static row => row.MonsterId);
        var extraItemsByMonster = GroupToLists(dropExtraItems, static row => row.MonsterId);
        var categoryRatesByMonster = GroupToLists(dropCategoryRates, static row => row.MonsterId);
        var result = new Dictionary<int, MonsterDefinition>(monsters.Count);

        foreach (var monster in monsters)
            result.Add(monster.MonsterId, new MonsterDefinition(
                monster,
                moneyByMonster.GetValueOrDefault(monster.MonsterId),
                TakeGroup(potionsByMonster, monster.MonsterId),
                TakeGroup(extraItemsByMonster, monster.MonsterId),
                TakeGroup(categoryRatesByMonster, monster.MonsterId),
                questItemByMonster.GetValueOrDefault(monster.MonsterId)));

        return result.ToFrozenDictionary();
    }

    /// <summary>Groups the 5 NPC child tables back per NpcId (the legacy NPC_INFO arrays).</summary>
    public static FrozenDictionary<int, NpcDefinition> BuildNpcs(
        IReadOnlyList<NpcRowDto> npcs,
        IReadOnlyList<NpcMenuOptionRowDto> menuOptions,
        IReadOnlyList<NpcShopItemRowDto> shopItems,
        IReadOnlyList<NpcSkillOfferRowDto> skillOffers,
        IReadOnlyList<NpcSpeechRowDto> speeches,
        IReadOnlyList<NpcGambleCostRowDto> gambleCosts)
    {
        var menuOptionsByNpc = GroupToLists(menuOptions, static row => row.NpcId);
        var shopItemsByNpc = GroupToLists(shopItems, static row => row.NpcId);
        var skillOffersByNpc = GroupToLists(skillOffers, static row => row.NpcId);
        var speechesByNpc = GroupToLists(speeches, static row => row.NpcId);
        var gambleCostsByNpc = GroupToLists(gambleCosts, static row => row.NpcId);
        var result = new Dictionary<int, NpcDefinition>(npcs.Count);

        foreach (var npc in npcs)
            result.Add(npc.NpcId, new NpcDefinition(
                npc,
                TakeGroup(menuOptionsByNpc, npc.NpcId),
                TakeGroup(shopItemsByNpc, npc.NpcId),
                TakeGroup(skillOffersByNpc, npc.NpcId),
                TakeGroup(speechesByNpc, npc.NpcId),
                TakeGroup(gambleCostsByNpc, npc.NpcId)));

        return result.ToFrozenDictionary();
    }

    /// <summary>Groups reward slots and dialogue lines back per QuestId.</summary>
    public static FrozenDictionary<int, QuestDefinition> BuildQuests(
        IReadOnlyList<QuestRowDto> quests,
        IReadOnlyList<QuestRewardRowDto> rewards,
        IReadOnlyList<QuestSpeechRowDto> speeches)
    {
        var rewardsByQuest = GroupToLists(rewards, static row => row.QuestId);
        var speechesByQuest = GroupToLists(speeches, static row => row.QuestId);
        var result = new Dictionary<int, QuestDefinition>(quests.Count);

        foreach (var quest in quests)
            result.Add(quest.QuestId, new QuestDefinition(
                quest,
                TakeGroup(rewardsByQuest, quest.QuestId),
                TakeGroup(speechesByQuest, quest.QuestId)));

        return result.ToFrozenDictionary();
    }

    /// <summary>
    ///     Filters portals with no destination, spawn regions with no zone/monster, and NPC placements with no NPC -- each discarded
    ///     row is counted in <see cref="WorldDataFilterStats" />. Landing points are all kept: a NULL FromZoneNumber just means "unrecorded source".
    /// </summary>
    public static (FrozenDictionary<short, ZoneDefinition> ZonesByNumber, WorldDataFilterStats Stats) BuildZones(
        IReadOnlyList<ZoneRowDto> zones,
        IReadOnlyList<ZonePortalRowDto> portals,
        IReadOnlyList<ZoneSpawnPointRowDto> spawnPoints,
        IReadOnlyList<ZoneNpcSpawnRowDto> npcSpawns,
        IReadOnlyList<MonsterSpawnRegionRowDto> spawnRegions)
    {
        var portalsWithoutDestination = 0;
        var portalsByZone = new Dictionary<short, List<ZonePortalRowDto>>();
        foreach (var portal in portals)
        {
            if (portal.TargetZoneNumber is null)
            {
                portalsWithoutDestination++;
                continue;
            }

            AddToGroup(portalsByZone, portal.ZoneNumber, portal);
        }

        var spawnPointsByZone = GroupToLists(spawnPoints, static row => row.ZoneNumber);

        var npcPlacementsWithoutNpc = 0;
        var npcSpawnsByZone = new Dictionary<short, List<ZoneNpcSpawnRowDto>>();
        foreach (var npcSpawn in npcSpawns)
        {
            if (npcSpawn.NpcId is null)
            {
                npcPlacementsWithoutNpc++;
                continue;
            }

            AddToGroup(npcSpawnsByZone, npcSpawn.ZoneNumber, npcSpawn);
        }

        var spawnRegionsWithoutZone = 0;
        var spawnRegionsWithoutMonster = 0;
        var spawnRegionsByZone = new Dictionary<short, List<MonsterSpawnRegionRowDto>>();
        foreach (var region in spawnRegions)
        {
            if (region.ZoneNumber is not { } zoneNumber)
            {
                spawnRegionsWithoutZone++;
                continue;
            }

            if (region.MonsterId is null)
            {
                spawnRegionsWithoutMonster++;
                continue;
            }

            AddToGroup(spawnRegionsByZone, zoneNumber, region);
        }

        var result = new Dictionary<short, ZoneDefinition>(zones.Count);
        foreach (var zone in zones)
            result.Add(zone.ZoneNumber, new ZoneDefinition(
                zone,
                TakeGroup(portalsByZone, zone.ZoneNumber),
                TakeGroup(spawnPointsByZone, zone.ZoneNumber),
                TakeGroup(npcSpawnsByZone, zone.ZoneNumber),
                TakeGroup(spawnRegionsByZone, zone.ZoneNumber)));

        var stats = new WorldDataFilterStats(
            portalsWithoutDestination,
            npcPlacementsWithoutNpc,
            spawnRegionsWithoutZone,
            spawnRegionsWithoutMonster);

        return (result.ToFrozenDictionary(), stats);
    }

    /// <summary>Groups populated bundle slots per RewardBundleId; a bundle with no slot rows keeps an empty array.</summary>
    public static FrozenDictionary<int, ImmutableArray<RewardBundleItemRowDto>> BuildRewardBundles(
        IReadOnlyList<RewardBundleRowDto> bundles,
        IReadOnlyList<RewardBundleItemRowDto> bundleItems)
    {
        var itemsByBundle = GroupToLists(bundleItems, static row => row.RewardBundleId);
        var result = new Dictionary<int, ImmutableArray<RewardBundleItemRowDto>>(bundles.Count);

        foreach (var bundle in bundles)
            result.Add(bundle.RewardBundleId, TakeGroup(itemsByBundle, bundle.RewardBundleId));

        return result.ToFrozenDictionary();
    }

    private static void EnsureCriticalDatasetNotEmpty(int rowCount, string datasetName)
    {
        if (rowCount == 0)
            throw new InvalidOperationException(
                $"Critical world dataset '{datasetName}' is empty -- the database is not seeded, and the " +
                "GameServer must not accept a single connection without its reference data (ADR-0011).");
    }

    private static Dictionary<TKey, List<TRow>> GroupToLists<TKey, TRow>(
        IReadOnlyList<TRow> rows,
        Func<TRow, TKey> keySelector)
        where TKey : notnull
    {
        var groups = new Dictionary<TKey, List<TRow>>();
        foreach (var row in rows)
            AddToGroup(groups, keySelector(row), row);

        return groups;
    }

    private static void AddToGroup<TKey, TRow>(Dictionary<TKey, List<TRow>> groups, TKey key, TRow row)
        where TKey : notnull
    {
        if (!groups.TryGetValue(key, out var list))
        {
            list = [];
            groups.Add(key, list);
        }

        list.Add(row);
    }

    private static ImmutableArray<TRow> TakeGroup<TKey, TRow>(Dictionary<TKey, List<TRow>> groups, TKey key)
        where TKey : notnull
    {
        return groups.TryGetValue(key, out var list)
            ? ImmutableArray.CreateRange(list)
            : ImmutableArray<TRow>.Empty;
    }
}
