using System.Collections.ObjectModel;

namespace Fenrir.Data.World;

/// <summary>Abstraction over Fenrir.Data.World.WorldDataRepository for DI/testability.</summary>
public interface IWorldDataRepository
{
    public ValueTask<(ReadOnlyCollection<ItemRowDto> Items, ReadOnlyCollection<ItemBonusSkillRowDto> BonusSkills)>
        GetItemsAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<MonsterRowDto>> GetMonstersAsync(CancellationToken ct);

    public ValueTask<(
            ReadOnlyCollection<MonsterDropMoneyRowDto> Money,
            ReadOnlyCollection<MonsterDropPotionRowDto> Potions,
            ReadOnlyCollection<MonsterDropExtraItemRowDto> ExtraItems,
            ReadOnlyCollection<MonsterDropCategoryRateRowDto> CategoryRates,
            ReadOnlyCollection<MonsterDropQuestItemRowDto> QuestItems)>
        GetMonsterDropsAsync(CancellationToken ct);

    public ValueTask<(
            ReadOnlyCollection<SkillRowDto> Skills,
            ReadOnlyCollection<SkillDescriptionRowDto> Descriptions,
            ReadOnlyCollection<SkillGradeRowDto> Grades)>
        GetSkillsAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<LevelRowDto>> GetLevelsAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<ZoneRowDto>> GetZonesAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<ZonePortalRowDto>> GetZonePortalsAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<ZoneSpawnPointRowDto>> GetZoneSpawnPointsAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<ZoneNpcSpawnRowDto>> GetZoneNpcSpawnsAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<MonsterSpawnRegionRowDto>> GetMonsterSpawnRegionsAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<NpcRowDto>> GetNpcsAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<NpcMenuOptionRowDto>> GetNpcMenuOptionsAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<NpcShopItemRowDto>> GetNpcShopItemsAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<NpcSkillOfferRowDto>> GetNpcSkillOffersAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<NpcSpeechRowDto>> GetNpcSpeechesAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<NpcGambleCostRowDto>> GetNpcGambleCostsAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<QuestRowDto>> GetQuestsAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<QuestRewardRowDto>> GetQuestRewardsAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<QuestSpeechRowDto>> GetQuestSpeechesAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<GemSocketRowDto>> GetGemSocketsAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<BloodExchangeCatalogRowDto>> GetBloodExchangeCatalogAsync(
        CancellationToken ct);

    public ValueTask<ReadOnlyCollection<EventDefinitionRowDto>> GetEventDefinitionsAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<ItemMallProductRowDto>> GetItemMallProductsAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<RewardBundleRowDto>> GetRewardBundlesAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<RewardBundleItemRowDto>> GetRewardBundleItemsAsync(CancellationToken ct);
}
