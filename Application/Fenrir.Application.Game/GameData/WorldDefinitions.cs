using System.Collections.Immutable;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.GameData;

/// <summary>
///     An item template with its bonus-skill slots re-attached (world.Items + world.ItemBonusSkills, zipped
///     back together from world.usp_Item_GetAll's two result sets). BonusSkills holds only the populated
///     slots, in SlotIndex order.
/// </summary>
public sealed record ItemDefinition(
    ItemRowDto Item,
    ImmutableArray<ItemBonusSkillRowDto> BonusSkills);

/// <summary>
///     A skill template with its description lines and its 2 grade rows (GradeIndex 0/1, the legacy
///     base/upgraded pair) re-attached from world.usp_Skill_GetAll's three result sets.
/// </summary>
public sealed record SkillDefinition(
    SkillRowDto Skill,
    ImmutableArray<SkillDescriptionRowDto> Descriptions,
    ImmutableArray<SkillGradeRowDto> Grades);

/// <summary>
///     A monster template with its 5 drop child tables grouped back per MonsterId (the drop pipeline order:
///     money, potions, extra items, category rates, quest item -- rapport 05 §drops). DropMoney and
///     DropQuestItem are at-most-one-per-monster in the legacy data, hence single nullable rows.
/// </summary>
public sealed record MonsterDefinition(
    MonsterRowDto Monster,
    MonsterDropMoneyRowDto? DropMoney,
    ImmutableArray<MonsterDropPotionRowDto> DropPotions,
    ImmutableArray<MonsterDropExtraItemRowDto> DropExtraItems,
    ImmutableArray<MonsterDropCategoryRateRowDto> DropCategoryRates,
    MonsterDropQuestItemRowDto? DropQuestItem);

/// <summary>
///     An NPC template with its 5 child tables grouped back per NpcId (menus, shop pages, skill offers,
///     speeches, gamble costs -- the legacy NPC_INFO arrays).
/// </summary>
public sealed record NpcDefinition(
    NpcRowDto Npc,
    ImmutableArray<NpcMenuOptionRowDto> MenuOptions,
    ImmutableArray<NpcShopItemRowDto> ShopItems,
    ImmutableArray<NpcSkillOfferRowDto> SkillOffers,
    ImmutableArray<NpcSpeechRowDto> Speeches,
    ImmutableArray<NpcGambleCostRowDto> GambleCosts);

/// <summary>
///     A quest template with its reward slots and dialogue lines grouped back per QuestId
///     (world.Quests + world.QuestRewards + world.QuestSpeeches).
/// </summary>
public sealed record QuestDefinition(
    QuestRowDto Quest,
    ImmutableArray<QuestRewardRowDto> Rewards,
    ImmutableArray<QuestSpeechRowDto> Speeches);

/// <summary>
///     Everything the simulation needs about one live zone: the default spawn (world.Zones), the outbound
///     portals that actually lead somewhere, the inbound landing points, the real NPC placements and the
///     monster spawn regions -- all pre-filtered of the legacy orphan rows at cache-build time (see
///     <see cref="WorldDataCacheBuilder.BuildZones" /> and <see cref="WorldDataFilterStats" />).
/// </summary>
public sealed record ZoneDefinition(
    ZoneRowDto Zone,
    ImmutableArray<ZonePortalRowDto> Portals,
    ImmutableArray<ZoneSpawnPointRowDto> SpawnPoints,
    ImmutableArray<ZoneNpcSpawnRowDto> NpcSpawns,
    ImmutableArray<MonsterSpawnRegionRowDto> MonsterSpawnRegions)
{
    /// <summary>
    ///     The landing point for an avatar arriving FROM the given zone (legacy StartCoordZone match), or null
    ///     when no slot names that source -- the caller then falls back to the zone's default spawn
    ///     (world.Zones.DefaultSpawnX/Y/Z), exactly like the legacy did for an unrecorded source.
    /// </summary>
    public ZoneSpawnPointRowDto? FindSpawnPointFrom(short fromZoneNumber)
    {
        foreach (var spawnPoint in SpawnPoints)
            if (spawnPoint.FromZoneNumber == fromZoneNumber)
                return spawnPoint;

        return null;
    }
}
