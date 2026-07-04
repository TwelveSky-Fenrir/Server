using System.Collections.Immutable;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.GameData;

/// <summary>BonusSkills holds only the populated slots, in SlotIndex order.</summary>
public sealed record ItemDefinition(
    ItemRowDto Item,
    ImmutableArray<ItemBonusSkillRowDto> BonusSkills);

/// <summary>Grades has the 2 rows (GradeIndex 0/1, the legacy base/upgraded pair).</summary>
public sealed record SkillDefinition(
    SkillRowDto Skill,
    ImmutableArray<SkillDescriptionRowDto> Descriptions,
    ImmutableArray<SkillGradeRowDto> Grades);

/// <summary>DropMoney and DropQuestItem are at-most-one-per-monster in the legacy data, hence single nullable rows.</summary>
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

/// <summary>Portals/spawns/NPC placements/monster regions are pre-filtered of legacy orphan rows at cache-build time -- see <see cref="WorldDataFilterStats" />.</summary>
public sealed record ZoneDefinition(
    ZoneRowDto Zone,
    ImmutableArray<ZonePortalRowDto> Portals,
    ImmutableArray<ZoneSpawnPointRowDto> SpawnPoints,
    ImmutableArray<ZoneNpcSpawnRowDto> NpcSpawns,
    ImmutableArray<MonsterSpawnRegionRowDto> MonsterSpawnRegions)
{
    /// <summary>Null when no slot names this source zone -- caller then falls back to the zone's default spawn.</summary>
    public ZoneSpawnPointRowDto? FindSpawnPointFrom(short fromZoneNumber)
    {
        foreach (var spawnPoint in SpawnPoints)
            if (spawnPoint.FromZoneNumber == fromZoneNumber)
                return spawnPoint;

        return null;
    }
}
