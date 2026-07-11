using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.GameData;

public sealed class WorldDataLoader(IWorldDataRepository repository, ILogger<WorldDataLoader> logger)
{
    private WorldDataCache? _cache;

        public WorldDataCache Cache => _cache ?? throw new InvalidOperationException(
        "WorldDataCache is not loaded yet -- call WorldDataLoader.InitializeAsync before accepting connections.");

        public async Task InitializeAsync(CancellationToken ct)
    {
        if (_cache is not null)
            throw new InvalidOperationException("WorldDataLoader.InitializeAsync must only be called once, at boot.");

        var stopwatch = Stopwatch.StartNew();

        var (items, itemBonusSkills) = await repository.GetItemsAsync(ct);
        var (skills, skillDescriptions, skillGrades) = await repository.GetSkillsAsync(ct);
        var monsters = await repository.GetMonstersAsync(ct);
        var (dropMoney, dropPotions, dropExtraItems, dropCategoryRates, dropQuestItems) =
            await repository.GetMonsterDropsAsync(ct);
        var npcs = await repository.GetNpcsAsync(ct);
        var npcMenuOptions = await repository.GetNpcMenuOptionsAsync(ct);
        var npcShopItems = await repository.GetNpcShopItemsAsync(ct);
        var npcSkillOffers = await repository.GetNpcSkillOffersAsync(ct);
        var npcSpeeches = await repository.GetNpcSpeechesAsync(ct);
        var npcGambleCosts = await repository.GetNpcGambleCostsAsync(ct);
        var quests = await repository.GetQuestsAsync(ct);
        var questRewards = await repository.GetQuestRewardsAsync(ct);
        var questSpeeches = await repository.GetQuestSpeechesAsync(ct);
        var levels = await repository.GetLevelsAsync(ct);
        var zones = await repository.GetZonesAsync(ct);
        var zonePortals = await repository.GetZonePortalsAsync(ct);
        var zoneSpawnPoints = await repository.GetZoneSpawnPointsAsync(ct);
        var zoneNpcSpawns = await repository.GetZoneNpcSpawnsAsync(ct);
        var monsterSpawnRegions = await repository.GetMonsterSpawnRegionsAsync(ct);
        var gemSockets = await repository.GetGemSocketsAsync(ct);
        var bloodExchangeCatalog = await repository.GetBloodExchangeCatalogAsync(ct);
        var eventDefinitions = await repository.GetEventDefinitionsAsync(ct);
        var itemMallProducts = await repository.GetItemMallProductsAsync(ct);
        var rewardBundles = await repository.GetRewardBundlesAsync(ct);
        var rewardBundleItems = await repository.GetRewardBundleItemsAsync(ct);

        var rows = new WorldDataRows
        {
            Items = items,
            ItemBonusSkills = itemBonusSkills,
            Skills = skills,
            SkillDescriptions = skillDescriptions,
            SkillGrades = skillGrades,
            Monsters = monsters,
            MonsterDropMoney = dropMoney,
            MonsterDropPotions = dropPotions,
            MonsterDropExtraItems = dropExtraItems,
            MonsterDropCategoryRates = dropCategoryRates,
            MonsterDropQuestItems = dropQuestItems,
            Npcs = npcs,
            NpcMenuOptions = npcMenuOptions,
            NpcShopItems = npcShopItems,
            NpcSkillOffers = npcSkillOffers,
            NpcSpeeches = npcSpeeches,
            NpcGambleCosts = npcGambleCosts,
            Quests = quests,
            QuestRewards = questRewards,
            QuestSpeeches = questSpeeches,
            Levels = levels,
            Zones = zones,
            ZonePortals = zonePortals,
            ZoneSpawnPoints = zoneSpawnPoints,
            ZoneNpcSpawns = zoneNpcSpawns,
            MonsterSpawnRegions = monsterSpawnRegions,
            GemSockets = gemSockets,
            BloodExchangeCatalog = bloodExchangeCatalog,
            EventDefinitions = eventDefinitions,
            ItemMallProducts = itemMallProducts,
            RewardBundles = rewardBundles,
            RewardBundleItems = rewardBundleItems
        };

        var (cache, stats) = WorldDataCacheBuilder.Build(rows);

        logger.LogInformation("World dataset loaded: {Count} items ({BonusSkills} bonus-skill slots)",
            cache.ItemsById.Count, itemBonusSkills.Count);
        logger.LogInformation(
            "World dataset loaded: {Count} skills ({Descriptions} description lines, {Grades} grade rows)",
            cache.SkillsById.Count, skillDescriptions.Count, skillGrades.Count);
        logger.LogInformation(
            "World dataset loaded: {Count} monsters ({Money} money, {Potions} potion, {Extras} extra-item, {Categories} category-rate, {QuestItems} quest-item drop rows)",
            cache.MonstersById.Count, dropMoney.Count, dropPotions.Count, dropExtraItems.Count,
            dropCategoryRates.Count, dropQuestItems.Count);
        logger.LogInformation(
            "World dataset loaded: {Count} NPCs ({Menus} menu slots, {ShopItems} shop slots, {SkillOffers} skill offers, {Speeches} speech lines, {GambleCosts} gamble-cost cells)",
            cache.NpcsById.Count, npcMenuOptions.Count, npcShopItems.Count, npcSkillOffers.Count,
            npcSpeeches.Count, npcGambleCosts.Count);
        logger.LogInformation(
            "World dataset loaded: {Count} quests ({Rewards} reward slots, {Speeches} dialogue lines)",
            cache.QuestsById.Count, questRewards.Count, questSpeeches.Count);
        logger.LogInformation("World dataset loaded: {Count} levels", cache.LevelsByLevel.Count);
        logger.LogInformation(
            "World dataset loaded: {Count} zones ({Portals} portals, {SpawnPoints} landing points, {NpcSpawns} NPC placements, {SpawnRegions} monster spawn regions kept)",
            cache.ZonesByNumber.Count,
            zonePortals.Count - stats.PortalsWithoutDestination,
            zoneSpawnPoints.Count,
            zoneNpcSpawns.Count - stats.NpcPlacementsWithoutNpc,
            monsterSpawnRegions.Count - stats.SpawnRegionsWithoutZone - stats.SpawnRegionsWithoutMonster);
        logger.LogInformation("World dataset loaded: {Count} gem sockets", cache.GemSocketsById.Count);
        logger.LogInformation(
            "World dataset loaded: {Blood} blood-exchange slots, {Events} event definitions, {MallProducts} item-mall products, {Bundles} reward bundles",
            cache.BloodExchangeCatalog.Length, cache.EventDefinitions.Length, cache.ItemMallProductsById.Count,
            cache.RewardBundleItemsByBundleId.Count);

        if (stats.TotalDiscarded > 0)
            logger.LogWarning(
                "World data orphan rows discarded (dead legacy references, rapport 05): {Portals} portals without destination, {NpcPlacements} NPC placements without NPC, {RegionsNoZone} spawn regions without zone, {RegionsNoMonster} spawn regions without monster",
                stats.PortalsWithoutDestination, stats.NpcPlacementsWithoutNpc,
                stats.SpawnRegionsWithoutZone, stats.SpawnRegionsWithoutMonster);

        _cache = cache;

        logger.LogInformation("WorldDataCache ready in {ElapsedMs:F0} ms", stopwatch.Elapsed.TotalMilliseconds);
    }
}
