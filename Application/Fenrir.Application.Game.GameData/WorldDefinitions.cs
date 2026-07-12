using System.Collections.Immutable;

namespace Fenrir.Application.Game.GameData;

public sealed record ItemDefinition(
    ItemRowDto Item,
    ImmutableArray<ItemBonusSkillRowDto> BonusSkills);

public sealed record SkillDefinition(
    SkillRowDto Skill,
    ImmutableArray<SkillDescriptionRowDto> Descriptions,
    ImmutableArray<SkillGradeRowDto> Grades);

public sealed record MonsterDefinition(
    MonsterRowDto Monster,
    MonsterDropMoneyRowDto? DropMoney,
    ImmutableArray<MonsterDropPotionRowDto> DropPotions,
    ImmutableArray<MonsterDropExtraItemRowDto> DropExtraItems,
    ImmutableArray<MonsterDropCategoryRateRowDto> DropCategoryRates,
    MonsterDropQuestItemRowDto? DropQuestItem);

public sealed record NpcDefinition(
    NpcRowDto Npc,
    ImmutableArray<NpcMenuOptionRowDto> MenuOptions,
    ImmutableArray<NpcShopItemRowDto> ShopItems,
    ImmutableArray<NpcSkillOfferRowDto> SkillOffers,
    ImmutableArray<NpcSpeechRowDto> Speeches,
    ImmutableArray<NpcGambleCostRowDto> GambleCosts);

public sealed record QuestDefinition(
    QuestRowDto Quest,
    ImmutableArray<QuestRewardRowDto> Rewards,
    ImmutableArray<QuestSpeechRowDto> Speeches);

public sealed record ZoneDefinition(
    ZoneRowDto Zone,
    ImmutableArray<ZonePortalRowDto> Portals,
    ImmutableArray<ZoneSpawnPointRowDto> SpawnPoints,
    ImmutableArray<ZoneNpcSpawnRowDto> NpcSpawns,
    ImmutableArray<MonsterSpawnRegionRowDto> MonsterSpawnRegions,
    ImmutableArray<MonsterSpawnRegionRowDto> BossMonsterSpawnRegions)
{
    public ZoneSpawnPointRowDto? FindSpawnPointFrom(short fromZoneNumber)
    {
        foreach (var spawnPoint in SpawnPoints)
            if (spawnPoint.FromZoneNumber == fromZoneNumber)
                return spawnPoint;

        return null;
    }
}
