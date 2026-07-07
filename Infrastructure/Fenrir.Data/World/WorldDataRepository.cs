using System.Collections.ObjectModel;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Data.World;

// Boot-time bulk reader over world.usp_*_GetAll; world.* is read-mostly, loaded once into an in-memory cache, never queried per-tick. Capacity hints are the real row counts.
public sealed record WorldDataRepository(ICaeriusNetDbContext Db) : IWorldDataRepository
{
    public async ValueTask<(ReadOnlyCollection<ItemRowDto> Items, ReadOnlyCollection<ItemBonusSkillRowDto> BonusSkills)>
        GetItemsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_Item_GetAll", 34_353).Build();

        return await Db.QueryMultipleReadOnlyCollectionAsync<ItemRowDto, ItemBonusSkillRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<MonsterRowDto>> GetMonstersAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_Monster_GetAll", 1_139).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<MonsterRowDto>(sp, ct);
    }

    // Fixed result-set order: money, potions, extra items, category rates, quest items.
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

    public async ValueTask<ReadOnlyCollection<LevelRowDto>> GetLevelsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_Level_GetAll", 145).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<LevelRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<ZoneRowDto>> GetZonesAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_Zone_GetAll", 117).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<ZoneRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<ZonePortalRowDto>> GetZonePortalsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_ZonePortal_GetAll", 413).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<ZonePortalRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<ZoneSpawnPointRowDto>> GetZoneSpawnPointsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_ZoneSpawnPoint_GetAll", 413).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<ZoneSpawnPointRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<ZoneNpcSpawnRowDto>> GetZoneNpcSpawnsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_ZoneNpcSpawn_GetAll", 291).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<ZoneNpcSpawnRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<MonsterSpawnRegionRowDto>> GetMonsterSpawnRegionsAsync(
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_MonsterSpawnRegion_GetAll", 21_960).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<MonsterSpawnRegionRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<NpcRowDto>> GetNpcsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_Npc_GetAll", 131).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<NpcRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<NpcMenuOptionRowDto>> GetNpcMenuOptionsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_NpcMenuOption_GetAll", 13_100).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<NpcMenuOptionRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<NpcShopItemRowDto>> GetNpcShopItemsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_NpcShopItem_GetAll", 467).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<NpcShopItemRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<NpcSkillOfferRowDto>> GetNpcSkillOffersAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_NpcSkillOffer_GetAll", 234).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<NpcSkillOfferRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<NpcSpeechRowDto>> GetNpcSpeechesAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_NpcSpeech_GetAll", 1_720).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<NpcSpeechRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<NpcGambleCostRowDto>> GetNpcGambleCostsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_NpcGambleCost_GetAll", 13_050).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<NpcGambleCostRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<QuestRowDto>> GetQuestsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_Quest_GetAll", 688).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<QuestRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<QuestRewardRowDto>> GetQuestRewardsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_QuestReward_GetAll", 1_434).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<QuestRewardRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<QuestSpeechRowDto>> GetQuestSpeechesAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_QuestSpeech_GetAll", 18_742).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<QuestSpeechRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<GemSocketRowDto>> GetGemSocketsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_GemSocket_GetAll", 2_891).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<GemSocketRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<BloodExchangeCatalogRowDto>> GetBloodExchangeCatalogAsync(
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_BloodExchangeCatalog_GetAll", 8).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<BloodExchangeCatalogRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<EventDefinitionRowDto>> GetEventDefinitionsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_EventDefinition_GetAll", 8).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<EventDefinitionRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<ItemMallProductRowDto>> GetItemMallProductsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_ItemMallProduct_GetAll", 512).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<ItemMallProductRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<RewardBundleRowDto>> GetRewardBundlesAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_RewardBundle_GetAll", 8).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<RewardBundleRowDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<RewardBundleItemRowDto>> GetRewardBundleItemsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_RewardBundleItem_GetAll", 8).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<RewardBundleItemRowDto>(sp, ct);
    }

    // Fixed result-set order: skill, item, costume equivalences. Capacity is the summed real row count
    // across all 3 result sets (40*3 + 131*3 + 27*3 = 594 -- see Tables/world/Tribe*Equivalences.sql).
    public async ValueTask<(
            ReadOnlyCollection<TribeSkillEquivalenceRowDto> SkillEquivalences,
            ReadOnlyCollection<TribeItemEquivalenceRowDto> ItemEquivalences,
            ReadOnlyCollection<TribeCostumeEquivalenceRowDto> CostumeEquivalences)>
        GetTribeConversionCatalogAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("world", "usp_TribeConversionCatalog_GetAll", 594).Build();

        return await Db.QueryMultipleReadOnlyCollectionAsync<
            TribeSkillEquivalenceRowDto, TribeItemEquivalenceRowDto, TribeCostumeEquivalenceRowDto>(sp, ct);
    }
}
