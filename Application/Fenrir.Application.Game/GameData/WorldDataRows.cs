using Fenrir.Data.World;

namespace Fenrir.Application.Game.GameData;

/// <summary>Raw, un-indexed rows from every world.usp_*_GetAll call -- <see cref="WorldDataCacheBuilder.Build" /> only ever sees this shape, never SQL.</summary>
public sealed record WorldDataRows
{
    public required IReadOnlyList<ItemRowDto> Items { get; init; }
    public required IReadOnlyList<ItemBonusSkillRowDto> ItemBonusSkills { get; init; }
    public required IReadOnlyList<SkillRowDto> Skills { get; init; }
    public required IReadOnlyList<SkillDescriptionRowDto> SkillDescriptions { get; init; }
    public required IReadOnlyList<SkillGradeRowDto> SkillGrades { get; init; }
    public required IReadOnlyList<MonsterRowDto> Monsters { get; init; }
    public required IReadOnlyList<MonsterDropMoneyRowDto> MonsterDropMoney { get; init; }
    public required IReadOnlyList<MonsterDropPotionRowDto> MonsterDropPotions { get; init; }
    public required IReadOnlyList<MonsterDropExtraItemRowDto> MonsterDropExtraItems { get; init; }
    public required IReadOnlyList<MonsterDropCategoryRateRowDto> MonsterDropCategoryRates { get; init; }
    public required IReadOnlyList<MonsterDropQuestItemRowDto> MonsterDropQuestItems { get; init; }
    public required IReadOnlyList<NpcRowDto> Npcs { get; init; }
    public required IReadOnlyList<NpcMenuOptionRowDto> NpcMenuOptions { get; init; }
    public required IReadOnlyList<NpcShopItemRowDto> NpcShopItems { get; init; }
    public required IReadOnlyList<NpcSkillOfferRowDto> NpcSkillOffers { get; init; }
    public required IReadOnlyList<NpcSpeechRowDto> NpcSpeeches { get; init; }
    public required IReadOnlyList<NpcGambleCostRowDto> NpcGambleCosts { get; init; }
    public required IReadOnlyList<QuestRowDto> Quests { get; init; }
    public required IReadOnlyList<QuestRewardRowDto> QuestRewards { get; init; }
    public required IReadOnlyList<QuestSpeechRowDto> QuestSpeeches { get; init; }
    public required IReadOnlyList<LevelRowDto> Levels { get; init; }
    public required IReadOnlyList<ZoneRowDto> Zones { get; init; }
    public required IReadOnlyList<ZonePortalRowDto> ZonePortals { get; init; }
    public required IReadOnlyList<ZoneSpawnPointRowDto> ZoneSpawnPoints { get; init; }
    public required IReadOnlyList<ZoneNpcSpawnRowDto> ZoneNpcSpawns { get; init; }
    public required IReadOnlyList<MonsterSpawnRegionRowDto> MonsterSpawnRegions { get; init; }
    public required IReadOnlyList<GemSocketRowDto> GemSockets { get; init; }
    public required IReadOnlyList<BloodExchangeCatalogRowDto> BloodExchangeCatalog { get; init; }
    public required IReadOnlyList<EventDefinitionRowDto> EventDefinitions { get; init; }
    public required IReadOnlyList<ItemMallProductRowDto> ItemMallProducts { get; init; }
    public required IReadOnlyList<RewardBundleRowDto> RewardBundles { get; init; }
    public required IReadOnlyList<RewardBundleItemRowDto> RewardBundleItems { get; init; }
}
