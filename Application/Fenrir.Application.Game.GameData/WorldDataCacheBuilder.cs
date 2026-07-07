using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Application.Game.GameData;

/// <summary>
///     Pure, SQL-free construction of <see cref="WorldDataCache" /> from raw world.* rows -- kept separate from the
///     loader so it's unit-testable on in-memory rows.
/// </summary>
public static class WorldDataCacheBuilder
{
    /// <summary>Legacy <c>MAX_LIMIT_LEVEL_NUM</c> -- see <see cref="ValidateLevels" /> for how it's used here.</summary>
    private const int MaxLevelIndex = 145;

    /// <summary>Legacy <c>Level_CheckValidElement</c>'s 0-10000 bound on every per-level combat stat field.</summary>
    private const int MaxLevelCombatStat = 10000;

    /// <summary>
    ///     Throws when a critical dataset (Items, Monsters, Zones, ZonePortals, Levels, Skills) is empty -- an
    ///     unseeded GameServer must not accept a single connection.
    /// </summary>
    /// <remarks>
    ///     Zone-transition parity note -- Réf. C++ : Server/Header/S19_MyZoneMoveInfo.cpp:42-58 (the
    ///     <c>ZONEMOVEINFO::Init()</c> branch active under <c>TS25_ZONE</c>, confirmed via
    ///     Server/ts25latest_general.props:15) ; Server/Header/S15_MyShare.cpp:47-69,771-812
    ///     (<c>MyShm::Init</c> / <c>MyShm::Load_ZoneMoveInfo</c>) ; Server/ts25sharemem/main.cpp:101-107.
    ///     Legacy ts25zone never reads <c>003.BIN</c> itself: it attaches to a cluster-wide named shared-memory
    ///     segment that only its first creator (architecturally the dedicated ts25sharemem process) populates
    ///     from disk on that boot of the cluster; every other process, including every zone shard, only
    ///     attaches to the already-populated segment. Fenrir has no equivalent shared segment to reproduce --
    ///     every GameServer shard independently loads <c>world.ZonePortals</c> (the normalized, one-row-per-exit
    ///     form of the legacy 350-slot zone-transition array) straight from SQL Server at its own boot, so the
    ///     "which process reads the file" race the legacy code tolerates simply does not exist here. What does
    ///     need reproducing is the failure contract: a missing or incomplete zone-transition dataset is fatal to
    ///     the whole boot, with no partial transition graph ever served (missing-file and short-read both fail
    ///     identically at S15_MyShare.cpp:771-812) -- hence <c>world.ZonePortals</c> joins the other
    ///     must-not-be-empty datasets below, checked only for whole-dataset presence and not per-row, matching
    ///     that legacy loader's own coarse, byte-count-only validation for this specific dataset (contrasted
    ///     there, at S15_MyShare.cpp:749-756/814-829, with the level/socket loaders' per-row checks).
    /// </remarks>
    public static (WorldDataCache Cache, WorldDataFilterStats Stats) Build(WorldDataRows rows)
    {
        EnsureCriticalDatasetNotEmpty(rows.Items.Count, "world.Items");
        EnsureCriticalDatasetNotEmpty(rows.Monsters.Count, "world.Monsters");
        EnsureCriticalDatasetNotEmpty(rows.Zones.Count, "world.Zones");
        EnsureCriticalDatasetNotEmpty(rows.ZonePortals.Count, "world.ZonePortals");
        EnsureCriticalDatasetNotEmpty(rows.Levels.Count, "world.Levels");
        EnsureCriticalDatasetNotEmpty(rows.Skills.Count, "world.Skills");

        // world.MonsterSpawnRegions is deliberately NOT in the critical-dataset list above -- see BuildZones'
        // own remarks for the WREGION-load parity this reproduces (a missing/empty spawn-region dataset must
        // never abort GameServer boot, only leave zones with fewer or zero monsters to spawn).
        ValidateLevels(rows.Levels);

        var (zonesByNumber, stats) = BuildZones(
            rows.Zones, rows.ZonePortals, rows.ZoneSpawnPoints, rows.ZoneNpcSpawns, rows.MonsterSpawnRegions);

        var cache = new WorldDataCache
        {
            ItemsById = BuildItems(rows.Items, rows.ItemBonusSkills),
            SkillsById = BuildSkills(rows.Skills, rows.SkillDescriptions, rows.SkillGrades),
            MonstersById = BuildMonsters(rows.Monsters, rows.MonsterDropMoney, rows.MonsterDropPotions,
                rows.MonsterDropExtraItems, rows.MonsterDropCategoryRates, rows.MonsterDropQuestItems),
            NpcsById = BuildNpcs(rows.Npcs, rows.NpcMenuOptions, rows.NpcShopItems, rows.NpcSkillOffers,
                rows.NpcSpeeches, rows.NpcGambleCosts),
            QuestsById = BuildQuests(rows.Quests, rows.QuestRewards, rows.QuestSpeeches),
            LevelsByLevel = rows.Levels.ToFrozenDictionary(static level => level.Level),
            ZonesByNumber = zonesByNumber,
            GemSocketsById = rows.GemSockets.ToFrozenDictionary(static gem => gem.GemSocketId),
            BloodExchangeCatalog = [.. rows.BloodExchangeCatalog],
            EventDefinitions = [.. rows.EventDefinitions],
            ItemMallProductsById =
                rows.ItemMallProducts.ToFrozenDictionary(static product => product.ItemMallProductId),
            RewardBundleItemsByBundleId = BuildRewardBundles(rows.RewardBundles, rows.RewardBundleItems),
            CashCatalog = CashCatalogBuilder.Build(rows.ItemMallProducts),
            CashCatalogVersion = CashCatalogBuilder.ResolveVersion(rows.ItemMallProducts)
        };

        return (cache, stats);
    }

    /// <summary>
    ///     Per-row LEVEL validation: aborts the whole load on the first invalid row instead of indexing whatever
    ///     was read, matching the legacy loader's no-skip-and-continue failure contract. This is the one piece of
    ///     the LEVEL/ITEM/SKILL/MONSTER/NPC/QUEST/GSOCKET "a single malformed row aborts the whole dataset"
    ///     parity gap closed here -- LEVEL is the only one of those seven systems whose per-row rule set
    ///     (<c>Level_CheckValidElement</c>) was read in full for the behavior contract backing this method.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/Header/S15_MyShare.cpp:373-421 (<c>Load_Level</c>, first-invalid-row abort, no
    ///     skip-and-continue) ; Server/Header/S15_MyShare.cpp:814-876 (<c>Level_CheckValidElement</c> -- index
    ///     bounds, index-equals-position+1, the two ExpRange bounds checks, an inter-row monotonicity check
    ///     against the next row for every index but the last, and 0-10000 bounds on every combat stat) ;
    ///     Server/Header/Protocol/DEFINE.h:604 (<c>MAX_LIMIT_LEVEL_NUM</c> = 145, reproduced below as
    ///     <see cref="MaxLevelIndex" />, the upper bound on <see cref="LevelRowDto.Level" />).
    ///     Deliberately NOT reproduced: the legacy loader also hard-fails unless the on-disk row count equals 145
    ///     exactly (<c>ZlibScope::Unpack005Copy</c>, Server/Header/Scope/ZlibScope.h:86-107) -- that is a
    ///     fixed-size-shared-memory-array sizing artifact with no Fenrir equivalent (same reasoning as the
    ///     ZonePortals SHM parity note on <see cref="Build" /> above), so a seed with fewer than 145 contiguous
    ///     levels is accepted here as long as every row it does have is internally consistent.
    ///     ITEM/SKILL/MONSTER/NPC/QUEST/GSOCKET intentionally get no per-row validation here: the behavior
    ///     contract backing this method explicitly flags those six systems' <c>*_CheckValidElement</c> rule sets
    ///     as unread/unverified, so nothing was guessed at for them -- closing that remains an open gap for a
    ///     follow-up <c>legacy-behavior-translator</c> contract per system.
    /// </remarks>
    private static void ValidateLevels(IReadOnlyList<LevelRowDto> levels)
    {
        var ordered = levels.OrderBy(static level => level.Level).ToArray();

        for (var index = 0; index < ordered.Length; index++)
        {
            var row = ordered[index];
            var expectedLevel = index + 1;

            if (row.Level != expectedLevel || row.Level > MaxLevelIndex)
                throw new InvalidOperationException(
                    $"world.Levels row at position {index} has Level={row.Level}, expected {expectedLevel} " +
                    $"-- rows must be contiguous starting at 1, and Level must never exceed {MaxLevelIndex}.");

            if (row.ExpRangeMin < 0 || row.ExpRangeMax < row.ExpRangeMin)
                throw new InvalidOperationException(
                    $"world.Levels row for Level={row.Level} has an invalid experience range " +
                    $"[{row.ExpRangeMin}, {row.ExpRangeMax}].");

            if (index < ordered.Length - 1 && ordered[index + 1].ExpRangeMin <= row.ExpRangeMax)
                throw new InvalidOperationException(
                    $"world.Levels row for Level={row.Level} (ExpRangeMax={row.ExpRangeMax}) must be strictly " +
                    $"below the next level's ExpRangeMin={ordered[index + 1].ExpRangeMin}.");

            if (row.AttackPower is < 0 or > MaxLevelCombatStat
                || row.DefensePower is < 0 or > MaxLevelCombatStat
                || row.AttackSuccess is < 0 or > MaxLevelCombatStat
                || row.AttackBlock is < 0 or > MaxLevelCombatStat
                || row.ElementAttack is < 0 or > MaxLevelCombatStat)
                throw new InvalidOperationException(
                    $"world.Levels row for Level={row.Level} has a combat stat outside the legacy " +
                    $"0-{MaxLevelCombatStat} bound.");
        }
    }

    /// <summary>Zips world.ItemBonusSkills back under its parent item (world.usp_Item_GetAll RS0+RS1).</summary>
    public static FrozenDictionary<int, ItemDefinition> BuildItems(
        IReadOnlyList<ItemRowDto> items,
        IReadOnlyList<ItemBonusSkillRowDto> bonusSkills)
    {
        var bonusSkillsByItem = GroupToLists(bonusSkills, static row => row.ItemId);
        var result = new Dictionary<int, ItemDefinition>(items.Count);

        foreach (var item in items)
            result.Add(item.ItemId, new ItemDefinition(item, TakeGroup(bonusSkillsByItem, item.ItemId)));

        return result.ToFrozenDictionary();
    }

    /// <summary>Zips description lines and the 2 grade rows back under each skill (world.usp_Skill_GetAll RS0-RS2).</summary>
    public static FrozenDictionary<int, SkillDefinition> BuildSkills(
        IReadOnlyList<SkillRowDto> skills,
        IReadOnlyList<SkillDescriptionRowDto> descriptions,
        IReadOnlyList<SkillGradeRowDto> grades)
    {
        var descriptionsBySkill = GroupToLists(descriptions, static row => row.SkillId);
        var gradesBySkill = GroupToLists(grades, static row => row.SkillId);
        var result = new Dictionary<int, SkillDefinition>(skills.Count);

        foreach (var skill in skills)
            result.Add(skill.SkillId, new SkillDefinition(
                skill,
                TakeGroup(descriptionsBySkill, skill.SkillId),
                TakeGroup(gradesBySkill, skill.SkillId)));

        return result.ToFrozenDictionary();
    }

    /// <summary>
    ///     Money/quest-item are at-most-one-per-monster in the legacy data; a duplicate would be a seed bug, so the plain
    ///     Add throws rather than silently picking one.
    /// </summary>
    public static FrozenDictionary<int, MonsterDefinition> BuildMonsters(
        IReadOnlyList<MonsterRowDto> monsters,
        IReadOnlyList<MonsterDropMoneyRowDto> dropMoney,
        IReadOnlyList<MonsterDropPotionRowDto> dropPotions,
        IReadOnlyList<MonsterDropExtraItemRowDto> dropExtraItems,
        IReadOnlyList<MonsterDropCategoryRateRowDto> dropCategoryRates,
        IReadOnlyList<MonsterDropQuestItemRowDto> dropQuestItems)
    {
        var moneyByMonster = new Dictionary<int, MonsterDropMoneyRowDto>(dropMoney.Count);
        foreach (var row in dropMoney)
            moneyByMonster.Add(row.MonsterId, row);

        var questItemByMonster = new Dictionary<int, MonsterDropQuestItemRowDto>(dropQuestItems.Count);
        foreach (var row in dropQuestItems)
            questItemByMonster.Add(row.MonsterId, row);

        var potionsByMonster = GroupToLists(dropPotions, static row => row.MonsterId);
        var extraItemsByMonster = GroupToLists(dropExtraItems, static row => row.MonsterId);
        var categoryRatesByMonster = GroupToLists(dropCategoryRates, static row => row.MonsterId);
        var result = new Dictionary<int, MonsterDefinition>(monsters.Count);

        foreach (var monster in monsters)
            result.Add(monster.MonsterId, new MonsterDefinition(
                monster,
                moneyByMonster.GetValueOrDefault(monster.MonsterId),
                TakeGroup(potionsByMonster, monster.MonsterId),
                TakeGroup(extraItemsByMonster, monster.MonsterId),
                TakeGroup(categoryRatesByMonster, monster.MonsterId),
                questItemByMonster.GetValueOrDefault(monster.MonsterId)));

        return result.ToFrozenDictionary();
    }

    /// <summary>Groups the 5 NPC child tables back per NpcId (the legacy NPC_INFO arrays).</summary>
    public static FrozenDictionary<int, NpcDefinition> BuildNpcs(
        IReadOnlyList<NpcRowDto> npcs,
        IReadOnlyList<NpcMenuOptionRowDto> menuOptions,
        IReadOnlyList<NpcShopItemRowDto> shopItems,
        IReadOnlyList<NpcSkillOfferRowDto> skillOffers,
        IReadOnlyList<NpcSpeechRowDto> speeches,
        IReadOnlyList<NpcGambleCostRowDto> gambleCosts)
    {
        var menuOptionsByNpc = GroupToLists(menuOptions, static row => row.NpcId);
        var shopItemsByNpc = GroupToLists(shopItems, static row => row.NpcId);
        var skillOffersByNpc = GroupToLists(skillOffers, static row => row.NpcId);
        var speechesByNpc = GroupToLists(speeches, static row => row.NpcId);
        var gambleCostsByNpc = GroupToLists(gambleCosts, static row => row.NpcId);
        var result = new Dictionary<int, NpcDefinition>(npcs.Count);

        foreach (var npc in npcs)
            result.Add(npc.NpcId, new NpcDefinition(
                npc,
                TakeGroup(menuOptionsByNpc, npc.NpcId),
                TakeGroup(shopItemsByNpc, npc.NpcId),
                TakeGroup(skillOffersByNpc, npc.NpcId),
                TakeGroup(speechesByNpc, npc.NpcId),
                TakeGroup(gambleCostsByNpc, npc.NpcId)));

        return result.ToFrozenDictionary();
    }

    /// <summary>Groups reward slots and dialogue lines back per QuestId.</summary>
    public static FrozenDictionary<int, QuestDefinition> BuildQuests(
        IReadOnlyList<QuestRowDto> quests,
        IReadOnlyList<QuestRewardRowDto> rewards,
        IReadOnlyList<QuestSpeechRowDto> speeches)
    {
        var rewardsByQuest = GroupToLists(rewards, static row => row.QuestId);
        var speechesByQuest = GroupToLists(speeches, static row => row.QuestId);
        var result = new Dictionary<int, QuestDefinition>(quests.Count);

        foreach (var quest in quests)
            result.Add(quest.QuestId, new QuestDefinition(
                quest,
                TakeGroup(rewardsByQuest, quest.QuestId),
                TakeGroup(speechesByQuest, quest.QuestId)));

        return result.ToFrozenDictionary();
    }

    /// <summary>
    ///     Filters portals with no destination, spawn regions with no zone/monster, and NPC placements with no NPC -- each
    ///     discarded
    ///     row is counted in <see cref="WorldDataFilterStats" />. Landing points are all kept: a NULL FromZoneNumber just
    ///     means "unrecorded source".
    /// </summary>
    /// <remarks>
    ///     NPC-placement parity note -- Réf. C++ : Server/ts25zone/S07_MyGame07.cpp:137-185 (<c>ZONENPCINFO::Init()</c>).
    ///     No per-row plausibility check runs on the surviving rows' coordinates/angle/NPC id -- the legacy loader
    ///     has no <c>*_CheckValidElement</c>-equivalent for this dataset (contrast Server/Header/S15_MyShare.cpp:402
    ///     etc. for LEVEL/ITEM/SKILL/MONSTER/NPC/QUEST/GSOCKET, which all have one), so a semantically-garbage-but-
    ///     present row is kept exactly as read here too, same as legacy. A zone-124-specific unconditional discard
    ///     was attempted here and reverted: the cited range's claim that legacy zeroes zone 124's NPC count on every
    ///     boot was never confirmed against whether "zone 124" means the game's actual ZoneNumber or an unrelated
    ///     internal array/slot index, and zone 124 is a real, live seeded zone with real NPC placements
    ///     (Database/Migrations/Seed/world/020_zones.sql, 021_zone_npc_spawns.sql) -- discarding them on an
    ///     unverified citation would be real production data loss, not a documented parity behavior.
    ///     <para>
    ///         Monster-spawn-region ("WREGION") parity note -- Réf. C++ : <c>MySummon::Init()</c>
    ///         (<c>Server/ts25zone/S10_MySummon.cpp:61-373</c>), called from <c>MyGame::Init()</c>
    ///         (<c>Server/ts25zone/S07_MyGame01.cpp:1658-1662</c>) strictly before the zone process ever accepts a
    ///         connection, but -- unlike every other dataset this method loads -- a missing or unparseable
    ///         <c>*.WREGION.csv</c> file is never fatal to boot there: the loader logs one diagnostic line and
    ///         silently leaves that file's monster category empty (
    ///         <c>
    ///             Server/ts25zone/S10_MySummon.cpp:476-479,
    ///             544-553,600-605
    ///         </c>
    ///         ), and the one caller-side check written to abort boot on a WREGION failure
    ///         (<c>MySummon::Init</c>'s own return value) structurally can never fire because <c>Init()</c>
    ///         unconditionally returns success regardless of either file's outcome
    ///         (<c>Server/ts25zone/S10_MySummon.cpp:372</c>). <c>spawnRegionsWithoutZone</c>/
    ///         <c>spawnRegionsWithoutMonster</c> below reproduce the same per-row silent-skip that
    ///         <c>LoadRegionInfo_1</c> applies when a row's monster reference does not resolve
    ///         (<c>Server/ts25zone/S10_MySummon.cpp:586-591</c>) -- one bad row (or, here, one bad/unshipped zone
    ///         reference) never discards the rest of the dataset -- and <see cref="Build" /> deliberately keeps
    ///         <c>world.MonsterSpawnRegions</c> off its must-not-be-empty list for the same reason. The
    ///         dungeon-density count bump and the whole-table capacity-overflow discard <c>LoadRegionInfo_1</c>
    ///         also applies are NOT reproduced at this filtering stage: see
    ///         <c>Fenrir.Application.Game.Domain.World.Monsters.MonsterSpawnScheduler</c>'s own remarks, where the
    ///         capacity check is applied per zone once spawn regions are resolved into runtime slots.
    ///     </para>
    /// </remarks>
    public static (FrozenDictionary<short, ZoneDefinition> ZonesByNumber, WorldDataFilterStats Stats) BuildZones(
        IReadOnlyList<ZoneRowDto> zones,
        IReadOnlyList<ZonePortalRowDto> portals,
        IReadOnlyList<ZoneSpawnPointRowDto> spawnPoints,
        IReadOnlyList<ZoneNpcSpawnRowDto> npcSpawns,
        IReadOnlyList<MonsterSpawnRegionRowDto> spawnRegions)
    {
        var portalsWithoutDestination = 0;
        var portalsByZone = new Dictionary<short, List<ZonePortalRowDto>>();
        foreach (var portal in portals)
        {
            if (portal.TargetZoneNumber is null)
            {
                portalsWithoutDestination++;
                continue;
            }

            AddToGroup(portalsByZone, portal.ZoneNumber, portal);
        }

        var spawnPointsByZone = GroupToLists(spawnPoints, static row => row.ZoneNumber);

        var npcPlacementsWithoutNpc = 0;
        var npcSpawnsByZone = new Dictionary<short, List<ZoneNpcSpawnRowDto>>();
        foreach (var npcSpawn in npcSpawns)
        {
            if (npcSpawn.NpcId is null)
            {
                npcPlacementsWithoutNpc++;
                continue;
            }

            AddToGroup(npcSpawnsByZone, npcSpawn.ZoneNumber, npcSpawn);
        }

        var spawnRegionsWithoutZone = 0;
        var spawnRegionsWithoutMonster = 0;
        var spawnRegionsByZone = new Dictionary<short, List<MonsterSpawnRegionRowDto>>();
        foreach (var region in spawnRegions)
        {
            if (region.ZoneNumber is not { } zoneNumber)
            {
                spawnRegionsWithoutZone++;
                continue;
            }

            if (region.MonsterId is null)
            {
                spawnRegionsWithoutMonster++;
                continue;
            }

            AddToGroup(spawnRegionsByZone, zoneNumber, region);
        }

        var result = new Dictionary<short, ZoneDefinition>(zones.Count);
        foreach (var zone in zones)
            result.Add(zone.ZoneNumber, new ZoneDefinition(
                zone,
                TakeGroup(portalsByZone, zone.ZoneNumber),
                TakeGroup(spawnPointsByZone, zone.ZoneNumber),
                TakeGroup(npcSpawnsByZone, zone.ZoneNumber),
                TakeGroup(spawnRegionsByZone, zone.ZoneNumber)));

        var stats = new WorldDataFilterStats(
            portalsWithoutDestination,
            npcPlacementsWithoutNpc,
            spawnRegionsWithoutZone,
            spawnRegionsWithoutMonster);

        return (result.ToFrozenDictionary(), stats);
    }

    /// <summary>Groups populated bundle slots per RewardBundleId; a bundle with no slot rows keeps an empty array.</summary>
    public static FrozenDictionary<int, ImmutableArray<RewardBundleItemRowDto>> BuildRewardBundles(
        IReadOnlyList<RewardBundleRowDto> bundles,
        IReadOnlyList<RewardBundleItemRowDto> bundleItems)
    {
        var itemsByBundle = GroupToLists(bundleItems, static row => row.RewardBundleId);
        var result = new Dictionary<int, ImmutableArray<RewardBundleItemRowDto>>(bundles.Count);

        foreach (var bundle in bundles)
            result.Add(bundle.RewardBundleId, TakeGroup(itemsByBundle, bundle.RewardBundleId));

        return result.ToFrozenDictionary();
    }

    private static void EnsureCriticalDatasetNotEmpty(int rowCount, string datasetName)
    {
        if (rowCount == 0)
            throw new InvalidOperationException(
                $"Critical world dataset '{datasetName}' is empty -- the database is not seeded, and the " +
                "GameServer must not accept a single connection without its reference data (ADR-0011).");
    }

    private static Dictionary<TKey, List<TRow>> GroupToLists<TKey, TRow>(
        IReadOnlyList<TRow> rows,
        Func<TRow, TKey> keySelector)
        where TKey : notnull
    {
        var groups = new Dictionary<TKey, List<TRow>>();
        foreach (var row in rows)
            AddToGroup(groups, keySelector(row), row);

        return groups;
    }

    private static void AddToGroup<TKey, TRow>(Dictionary<TKey, List<TRow>> groups, TKey key, TRow row)
        where TKey : notnull
    {
        if (!groups.TryGetValue(key, out var list))
        {
            list = [];
            groups.Add(key, list);
        }

        list.Add(row);
    }

    private static ImmutableArray<TRow> TakeGroup<TKey, TRow>(Dictionary<TKey, List<TRow>> groups, TKey key)
        where TKey : notnull
    {
        return groups.TryGetValue(key, out var list)
            ? ImmutableArray.CreateRange(list)
            : ImmutableArray<TRow>.Empty;
    }
}
