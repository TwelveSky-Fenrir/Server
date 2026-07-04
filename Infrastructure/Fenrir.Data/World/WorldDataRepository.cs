using System.Collections.ObjectModel;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;

namespace Fenrir.Data.World;

/// <summary>
///     Boot-time bulk reader over every world.usp_*_GetAll procedure (architecture reference §11.1, ADR-0011:
///     world.* is read-mostly reference data, loaded once at GameServer startup into an in-memory cache and
///     never queried per-game-tick). Singleton, injected only with ICaeriusNetDbContext -- no SqlDbType or
///     builder ever leaks past this type; callers see typed ValueTasks only. Capacity hints are the real row
///     counts documented in each procedure's contract header.
/// </summary>
public sealed record WorldDataRepository(ICaeriusNetDbContext Db) : IWorldDataRepository
{
    /// <summary>world.usp_Item_GetAll: RS0 = one row per item (34,353), RS1 = populated bonus-skill slots.</summary>
    public async ValueTask<(ReadOnlyCollection<ItemRowDto> Items, ReadOnlyCollection<ItemBonusSkillRowDto> BonusSkills)>
        GetItemsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_Item_GetAll", 34_353).Build();

        return await Db.QueryMultipleReadOnlyCollectionAsync<ItemRowDto, ItemBonusSkillRowDto>(sp, ct);
    }

    /// <summary>world.usp_Monster_GetAll: one row per monster (1,139), animation frames pre-joined 1:1.</summary>
    public async ValueTask<ReadOnlyCollection<MonsterRowDto>> GetMonstersAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_Monster_GetAll", 1_139).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<MonsterRowDto>(sp, ct);
    }

    /// <summary>
    ///     world.usp_Monster_GetDrops: the 5 drop child tables in one round trip, in the procedure's fixed
    ///     result-set order (money, potions, extra items, category rates, quest items).
    /// </summary>
    public async ValueTask<(
            ReadOnlyCollection<MonsterDropMoneyRowDto> Money,
            ReadOnlyCollection<MonsterDropPotionRowDto> Potions,
            ReadOnlyCollection<MonsterDropExtraItemRowDto> ExtraItems,
            ReadOnlyCollection<MonsterDropCategoryRateRowDto> CategoryRates,
            ReadOnlyCollection<MonsterDropQuestItemRowDto> QuestItems)>
        GetMonsterDropsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_Monster_GetDrops", 13_686).Build();

        return await Db.QueryMultipleReadOnlyCollectionAsync<
            MonsterDropMoneyRowDto,
            MonsterDropPotionRowDto,
            MonsterDropExtraItemRowDto,
            MonsterDropCategoryRateRowDto,
            MonsterDropQuestItemRowDto>(sp, ct);
    }

    /// <summary>world.usp_Skill_GetAll: RS0 = one row per skill (153), RS1 = description lines, RS2 = the 2 grades per skill.</summary>
    public async ValueTask<(
            ReadOnlyCollection<SkillRowDto> Skills,
            ReadOnlyCollection<SkillDescriptionRowDto> Descriptions,
            ReadOnlyCollection<SkillGradeRowDto> Grades)>
        GetSkillsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_Skill_GetAll", 1_071).Build();

        return await Db.QueryMultipleReadOnlyCollectionAsync<SkillRowDto, SkillDescriptionRowDto, SkillGradeRowDto>(sp,
            ct);
    }

    /// <summary>world.usp_Level_GetAll: one row per level (145), ordered ascending.</summary>
    public async ValueTask<ReadOnlyCollection<LevelRowDto>> GetLevelsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_Level_GetAll", 145).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<LevelRowDto>(sp, ct);
    }

    /// <summary>world.usp_Zone_GetAll: one row per live zone (117).</summary>
    public async ValueTask<ReadOnlyCollection<ZoneRowDto>> GetZonesAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_Zone_GetAll", 117).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<ZoneRowDto>(sp, ct);
    }

    /// <summary>world.usp_ZonePortal_GetAll: one row per populated outbound-portal slot (413).</summary>
    public async ValueTask<ReadOnlyCollection<ZonePortalRowDto>> GetZonePortalsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_ZonePortal_GetAll", 413).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<ZonePortalRowDto>(sp, ct);
    }

    /// <summary>world.usp_ZoneSpawnPoint_GetAll: one row per populated inbound-landing slot (413).</summary>
    public async ValueTask<ReadOnlyCollection<ZoneSpawnPointRowDto>> GetZoneSpawnPointsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_ZoneSpawnPoint_GetAll", 413).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<ZoneSpawnPointRowDto>(sp, ct);
    }

    /// <summary>world.usp_ZoneNpcSpawn_GetAll: one row per populated NPC-placement slot (291).</summary>
    public async ValueTask<ReadOnlyCollection<ZoneNpcSpawnRowDto>> GetZoneNpcSpawnsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_ZoneNpcSpawn_GetAll", 291).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<ZoneNpcSpawnRowDto>(sp, ct);
    }

    /// <summary>world.usp_MonsterSpawnRegion_GetAll: every spawn-region row (21,960 -- ~49% with a NULL ZoneNumber).</summary>
    public async ValueTask<ReadOnlyCollection<MonsterSpawnRegionRowDto>> GetMonsterSpawnRegionsAsync(
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_MonsterSpawnRegion_GetAll", 21_960).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<MonsterSpawnRegionRowDto>(sp, ct);
    }

    /// <summary>world.usp_Npc_GetAll: one row per real NPC (131).</summary>
    public async ValueTask<ReadOnlyCollection<NpcRowDto>> GetNpcsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_Npc_GetAll", 131).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<NpcRowDto>(sp, ct);
    }

    /// <summary>world.usp_NpcMenuOption_GetAll: one row per menu slot (13,100).</summary>
    public async ValueTask<ReadOnlyCollection<NpcMenuOptionRowDto>> GetNpcMenuOptionsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_NpcMenuOption_GetAll", 13_100).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<NpcMenuOptionRowDto>(sp, ct);
    }

    /// <summary>world.usp_NpcShopItem_GetAll: one row per populated shop slot (467).</summary>
    public async ValueTask<ReadOnlyCollection<NpcShopItemRowDto>> GetNpcShopItemsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_NpcShopItem_GetAll", 467).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<NpcShopItemRowDto>(sp, ct);
    }

    /// <summary>world.usp_NpcSkillOffer_GetAll: one row per populated skill-offer slot (234).</summary>
    public async ValueTask<ReadOnlyCollection<NpcSkillOfferRowDto>> GetNpcSkillOffersAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_NpcSkillOffer_GetAll", 234).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<NpcSkillOfferRowDto>(sp, ct);
    }

    /// <summary>world.usp_NpcSpeech_GetAll: one row per populated speech line (1,720).</summary>
    public async ValueTask<ReadOnlyCollection<NpcSpeechRowDto>> GetNpcSpeechesAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_NpcSpeech_GetAll", 1_720).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<NpcSpeechRowDto>(sp, ct);
    }

    /// <summary>world.usp_NpcGambleCost_GetAll: one row per gamble-cost cell (13,050).</summary>
    public async ValueTask<ReadOnlyCollection<NpcGambleCostRowDto>> GetNpcGambleCostsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_NpcGambleCost_GetAll", 13_050).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<NpcGambleCostRowDto>(sp, ct);
    }

    /// <summary>world.usp_Quest_GetAll: one row per quest (688).</summary>
    public async ValueTask<ReadOnlyCollection<QuestRowDto>> GetQuestsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_Quest_GetAll", 688).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<QuestRowDto>(sp, ct);
    }

    /// <summary>world.usp_QuestReward_GetAll: one row per populated reward slot (1,434).</summary>
    public async ValueTask<ReadOnlyCollection<QuestRewardRowDto>> GetQuestRewardsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_QuestReward_GetAll", 1_434).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<QuestRewardRowDto>(sp, ct);
    }

    /// <summary>world.usp_QuestSpeech_GetAll: one row per non-empty dialogue line (18,742).</summary>
    public async ValueTask<ReadOnlyCollection<QuestSpeechRowDto>> GetQuestSpeechesAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_QuestSpeech_GetAll", 18_742).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<QuestSpeechRowDto>(sp, ct);
    }

    /// <summary>world.usp_GemSocket_GetAll: one row per gem-socket definition (2,891).</summary>
    public async ValueTask<ReadOnlyCollection<GemSocketRowDto>> GetGemSocketsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_GemSocket_GetAll", 2_891).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<GemSocketRowDto>(sp, ct);
    }

    /// <summary>world.usp_BloodExchangeCatalog_GetAll: every blood-exchange slot (3 real rows).</summary>
    public async ValueTask<ReadOnlyCollection<BloodExchangeCatalogRowDto>> GetBloodExchangeCatalogAsync(
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_BloodExchangeCatalog_GetAll", 8).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<BloodExchangeCatalogRowDto>(sp, ct);
    }

    /// <summary>world.usp_EventDefinition_GetAll: every event definition (0 rows in the legacy dump).</summary>
    public async ValueTask<ReadOnlyCollection<EventDefinitionRowDto>> GetEventDefinitionsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_EventDefinition_GetAll", 8).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<EventDefinitionRowDto>(sp, ct);
    }

    /// <summary>world.usp_ItemMallProduct_GetAll: every cash-shop product, active and inactive.</summary>
    public async ValueTask<ReadOnlyCollection<ItemMallProductRowDto>> GetItemMallProductsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_ItemMallProduct_GetAll", 512).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<ItemMallProductRowDto>(sp, ct);
    }

    /// <summary>world.usp_RewardBundle_GetAll: every bundle id (1 row in this build).</summary>
    public async ValueTask<ReadOnlyCollection<RewardBundleRowDto>> GetRewardBundlesAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_RewardBundle_GetAll", 8).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<RewardBundleRowDto>(sp, ct);
    }

    /// <summary>world.usp_RewardBundleItem_GetAll: every populated bundle slot.</summary>
    public async ValueTask<ReadOnlyCollection<RewardBundleItemRowDto>> GetRewardBundleItemsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_RewardBundleItem_GetAll", 8).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<RewardBundleItemRowDto>(sp, ct);
    }
}
