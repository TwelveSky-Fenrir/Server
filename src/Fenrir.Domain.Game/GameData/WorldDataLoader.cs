using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Fenrir.Domain.Game.GameData;

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

        var itemsTask = repository.GetItemsAsync(ct);
        var skillsTask = repository.GetSkillsAsync(ct);
        var monstersTask = repository.GetMonstersAsync(ct);
        var monsterDropsTask = repository.GetMonsterDropsAsync(ct);
        var npcsTask = repository.GetNpcsAsync(ct);
        var npcMenuOptionsTask = repository.GetNpcMenuOptionsAsync(ct);
        var npcShopItemsTask = repository.GetNpcShopItemsAsync(ct);
        var npcSkillOffersTask = repository.GetNpcSkillOffersAsync(ct);
        var npcSpeechesTask = repository.GetNpcSpeechesAsync(ct);
        var npcGambleCostsTask = repository.GetNpcGambleCostsAsync(ct);
        var questsTask = repository.GetQuestsAsync(ct);
        var questRewardsTask = repository.GetQuestRewardsAsync(ct);
        var questSpeechesTask = repository.GetQuestSpeechesAsync(ct);
        var levelsTask = repository.GetLevelsAsync(ct);
        var zonesTask = repository.GetZonesAsync(ct);
        var zonePortalsTask = repository.GetZonePortalsAsync(ct);
        var zoneSpawnPointsTask = repository.GetZoneSpawnPointsAsync(ct);
        var zoneNpcSpawnsTask = repository.GetZoneNpcSpawnsAsync(ct);
        var monsterSpawnRegionsTask = repository.GetMonsterSpawnRegionsAsync(ct);
        var gemSocketsTask = repository.GetGemSocketsAsync(ct);
        var bloodExchangeCatalogTask = repository.GetBloodExchangeCatalogAsync(ct);
        var eventDefinitionsTask = repository.GetEventDefinitionsAsync(ct);
        var itemMallProductsTask = repository.GetItemMallProductsAsync(ct);
        var rewardBundlesTask = repository.GetRewardBundlesAsync(ct);
        var rewardBundleItemsTask = repository.GetRewardBundleItemsAsync(ct);

        await Task.WhenAll(
            itemsTask.AsTask(), skillsTask.AsTask(), monstersTask.AsTask(), monsterDropsTask.AsTask(),
            npcsTask.AsTask(), npcMenuOptionsTask.AsTask(), npcShopItemsTask.AsTask(), npcSkillOffersTask.AsTask(),
            npcSpeechesTask.AsTask(), npcGambleCostsTask.AsTask(), questsTask.AsTask(), questRewardsTask.AsTask(),
            questSpeechesTask.AsTask(), levelsTask.AsTask(), zonesTask.AsTask(), zonePortalsTask.AsTask(),
            zoneSpawnPointsTask.AsTask(), zoneNpcSpawnsTask.AsTask(), monsterSpawnRegionsTask.AsTask(),
            gemSocketsTask.AsTask(), bloodExchangeCatalogTask.AsTask(), eventDefinitionsTask.AsTask(),
            itemMallProductsTask.AsTask(), rewardBundlesTask.AsTask(), rewardBundleItemsTask.AsTask());

        var (items, itemBonusSkills) = itemsTask.Result;
        var (skills, skillDescriptions, skillGrades) = skillsTask.Result;
        var monsters = monstersTask.Result;
        var (dropMoney, dropPotions, dropExtraItems, dropCategoryRates, dropQuestItems) = monsterDropsTask.Result;
        var npcs = npcsTask.Result;
        var npcMenuOptions = npcMenuOptionsTask.Result;
        var npcShopItems = npcShopItemsTask.Result;
        var npcSkillOffers = npcSkillOffersTask.Result;
        var npcSpeeches = npcSpeechesTask.Result;
        var npcGambleCosts = npcGambleCostsTask.Result;
        var quests = questsTask.Result;
        var questRewards = questRewardsTask.Result;
        var questSpeeches = questSpeechesTask.Result;
        var levels = levelsTask.Result;
        var zones = zonesTask.Result;
        var zonePortals = zonePortalsTask.Result;
        var zoneSpawnPoints = zoneSpawnPointsTask.Result;
        var zoneNpcSpawns = zoneNpcSpawnsTask.Result;
        var monsterSpawnRegions = monsterSpawnRegionsTask.Result;
        var gemSockets = gemSocketsTask.Result;
        var bloodExchangeCatalog = bloodExchangeCatalogTask.Result;
        var eventDefinitions = eventDefinitionsTask.Result;
        var itemMallProducts = itemMallProductsTask.Result;
        var rewardBundles = rewardBundlesTask.Result;
        var rewardBundleItems = rewardBundleItemsTask.Result;

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
