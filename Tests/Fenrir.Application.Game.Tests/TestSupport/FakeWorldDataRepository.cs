using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeWorldDataRepository : IWorldDataRepository
{
    public List<ItemMallProductRowDto> ItemMallProducts { get; set; } = [];
    public List<BloodExchangeCatalogRowDto> BloodExchangeCatalog { get; set; } = [];

    public bool ThrowOnGetItemMallProducts { get; set; }
    public bool ThrowOnGetBloodExchangeCatalog { get; set; }

    public ValueTask<ReadOnlyCollection<ItemMallProductRowDto>> GetItemMallProductsAsync(CancellationToken ct)
    {
        if (ThrowOnGetItemMallProducts)
            throw new InvalidOperationException("Simulated SQL failure");

        return ValueTask.FromResult(new ReadOnlyCollection<ItemMallProductRowDto>(ItemMallProducts));
    }

    public ValueTask<ReadOnlyCollection<BloodExchangeCatalogRowDto>> GetBloodExchangeCatalogAsync(CancellationToken ct)
    {
        if (ThrowOnGetBloodExchangeCatalog)
            throw new InvalidOperationException("Simulated SQL failure");

        return ValueTask.FromResult(new ReadOnlyCollection<BloodExchangeCatalogRowDto>(BloodExchangeCatalog));
    }

    public ValueTask<(ReadOnlyCollection<ItemRowDto> Items, ReadOnlyCollection<ItemBonusSkillRowDto> BonusSkills)>
        GetItemsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<MonsterRowDto>> GetMonstersAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<(
            ReadOnlyCollection<MonsterDropMoneyRowDto> Money,
            ReadOnlyCollection<MonsterDropPotionRowDto> Potions,
            ReadOnlyCollection<MonsterDropExtraItemRowDto> ExtraItems,
            ReadOnlyCollection<MonsterDropCategoryRateRowDto> CategoryRates,
            ReadOnlyCollection<MonsterDropQuestItemRowDto> QuestItems)>
        GetMonsterDropsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<(
            ReadOnlyCollection<SkillRowDto> Skills,
            ReadOnlyCollection<SkillDescriptionRowDto> Descriptions,
            ReadOnlyCollection<SkillGradeRowDto> Grades)>
        GetSkillsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<LevelRowDto>> GetLevelsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<ZoneRowDto>> GetZonesAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<ZonePortalRowDto>> GetZonePortalsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<ZoneSpawnPointRowDto>> GetZoneSpawnPointsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<ZoneNpcSpawnRowDto>> GetZoneNpcSpawnsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<MonsterSpawnRegionRowDto>> GetMonsterSpawnRegionsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<NpcRowDto>> GetNpcsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<NpcMenuOptionRowDto>> GetNpcMenuOptionsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<NpcShopItemRowDto>> GetNpcShopItemsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<NpcSkillOfferRowDto>> GetNpcSkillOffersAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<NpcSpeechRowDto>> GetNpcSpeechesAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<NpcGambleCostRowDto>> GetNpcGambleCostsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<QuestRowDto>> GetQuestsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<QuestRewardRowDto>> GetQuestRewardsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<QuestSpeechRowDto>> GetQuestSpeechesAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<GemSocketRowDto>> GetGemSocketsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<EventDefinitionRowDto>> GetEventDefinitionsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<RewardBundleRowDto>> GetRewardBundlesAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ReadOnlyCollection<RewardBundleItemRowDto>> GetRewardBundleItemsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<(
            ReadOnlyCollection<TribeSkillEquivalenceRowDto> SkillEquivalences,
            ReadOnlyCollection<TribeItemEquivalenceRowDto> ItemEquivalences,
            ReadOnlyCollection<TribeCostumeEquivalenceRowDto> CostumeEquivalences)>
        GetTribeConversionCatalogAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
