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

    /// <summary>
    ///     Legacy <c>Level_CheckValidElement</c>'s 0-10000 bound, applied identically to every per-level combat
    ///     stat field (AttackPower/DefensePower/AttackSuccess/AttackBlock/ElementAttack) and, per the same
    ///     function, to Life and Mana too (Server/Header/S15_MyShare.cpp:847-874).
    /// </summary>
    private const int MaxLevelCombatStat = 10000;

    /// <summary>
    ///     Legacy <c>MAX_NUMBER_SIZE</c> -- upper bound on both ExpRangeMin and ExpRangeMax
    ///     (Server/Header/Protocol/DEFINE.h:365, applied at Server/Header/S15_MyShare.cpp:824-831).
    /// </summary>
    private const int MaxExpRangeBound = 2_000_000_000;

    /// <summary>Legacy <c>Level_CheckValidElement</c>'s 0-100 bound on RangeInfo3 (Server/Header/S15_MyShare.cpp:843-846).</summary>
    private const int MaxRangeInfo3 = 100;

    /// <summary>Legacy <c>Quest_CheckValidElement</c>'s 1-1000 bound on Step (Server/Header/S15_MyShare.cpp:2003-2007).</summary>
    private const int MinQuestStep = 1;

    private const int MaxQuestStep = 1000;

    /// <summary>
    ///     Legacy <c>Quest_CheckValidElement</c>'s 0-100,000,000 bound on reward Amount (Server/Header/S15_MyShare.cpp:
    ///     2044-2054); nullable-aware since Amount is unset for RewardType 6 (item) reward slots.
    /// </summary>
    private const int MinQuestRewardAmount = 0;

    private const int MaxQuestRewardAmount = 100_000_000;

    /// <summary>
    ///     Legacy <c>Item_CheckValidElement</c>'s 0-365 bound on CheckDateItem (Server/Header/S15_MyShare.cpp:
    ///     1065-1069) -- despite the "Check" prefix and world.Items' own stale inline comment (which claims
    ///     0-30), this is a day count, not a boolean.
    /// </summary>
    private const int MaxItemCheckDateItem = 365;

    /// <summary>
    ///     Legacy <c>MAX_ITEM_STATUS_ATTRIBUTE</c> (Server/Header/S15_MyShare.cpp:880-888), the 0-10000 bound
    ///     <c>Item_CheckValidElement</c> applies to DataNumber3D/AddDataNumber3D (Server/Header/S15_MyShare.cpp:
    ///     950-959) and, in the default (non-restricted) branch, to PotionType2 (see
    ///     <see cref="MaxPotionType2Default" />).
    /// </summary>
    private const int MaxItemDataNumber3D = 10000;

    /// <summary>
    ///     Legacy <c>Item_CheckValidElement</c>'s conditional bound on PotionType2 (Server/Header/S15_MyShare.cpp:
    ///     1130-1147): PotionType2 is restricted to 1-3 whenever PotionType1 equals this value, and to 0-
    ///     <see cref="MaxItemDataNumber3D" /> (<c>MAX_ITEM_STATUS_ATTRIBUTE</c>) otherwise.
    /// </summary>
    private const int PotionType1RestrictedValue = 9;

    private const int MinPotionType2Restricted = 1;
    private const int MaxPotionType2Restricted = 3;
    private const int MaxPotionType2Default = MaxItemDataNumber3D;

    /// <summary>
    ///     Legacy <c>GSocket_CheckValidElement</c>'s Type range gate (Server/Header/S15_MyShare.cpp:2218) -- see
    ///     <see cref="ValidateGemSockets" /> for the full per-band rule set this bounds.
    /// </summary>
    private const int MinGemSocketType = 1;

    private const int MaxGemSocketType = 46;

    // Monster_CheckValidElement bounds (Server/Header/S15_MyShare.cpp:1519-1830) -- see ValidateMonsters for how
    // each of these is used; grouped/named to mirror the CK_Monsters_*/CK_MonsterDrop*_* constraint groupings
    // added by Database/Migrations/036_monster_static_data_range_checks.sql, which cites the same line ranges
    // per group.
    private const int MaxMonsterNameLength = 24;
    private const int MaxMonsterChatLineLength = 100;
    private const int MinMonsterType = 1;
    private const int MaxMonsterType = 15;
    private const int MinMonsterSpecialType = 1;
    private const int MaxMonsterSpecialType = 53;
    private const int MinMonsterDamageType = 1;
    private const int MaxMonsterDamageType = 2;
    private const int MinMonsterDataSortNumber = 1;
    private const int MaxMonsterDataSortNumber = 10000;
    private const int MinMonsterSize = 1;
    private const int MaxMonsterSize = 1000;
    private const int MaxMonsterSize4 = 1000;
    private const int MinMonsterSizeCategory = 1;
    private const int MaxMonsterSizeCategory = 4;
    private const int MinMonsterCheckCollision = 1;
    private const int MaxMonsterCheckCollision = 2;
    private const int MaxMonsterHitCount = 3;
    private const int MinMonsterItemLevel = 1;
    private const int MaxMonsterItemLevel = 145;
    private const int MaxMonsterMartialItemLevel = 25;
    private const int MinMonsterRealLevel = 1;
    private const int MaxMonsterRealLevel = 1000;
    private const int MaxMonsterMartialRealLevel = 1000;
    private const int MaxMonsterExperienceReward = 100_000_000;
    private const int MinMonsterLife = 1;
    private const int MaxMonsterLife = 2_000_000_000;
    private const int MinMonsterAttackType = 1;
    private const int MaxMonsterAttackType = 6;
    private const int MaxMonsterRadiusInfo = 10000;
    private const int MaxMonsterMovementSpeed = 1000;
    private const int MaxMonsterAttackPower = 1_000_000;
    private const int MaxMonsterCombatStat = 100_000;
    private const int MaxMonsterCritical = 100;
    private const int MaxMonsterFollowInfo = 100;
    private const int MinMonsterSummonTime = 1;
    private const int MaxMonsterSummonTime = 1_000_000;
    private const int MinMonsterFrameInfo = 1;
    private const int MaxMonsterFrameInfo = 10000;
    private const int MaxMonsterHitFrame = 10000;

    /// <summary>
    ///     The legacy per-million drop-rate ceiling (<c>DropRate BETWEEN 0 AND 1000000</c>), identical across all
    ///     five MonsterDrop* child tables (Server/Header/S15_MyShare.cpp:1768-1818).
    /// </summary>
    private const int MaxMonsterDropRate = 1_000_000;

    /// <summary>
    ///     The legacy item-id space ceiling shared by PotionItemId/ItemId/QuestItemId across the drop tables that
    ///     reference an item (Server/Header/S15_MyShare.cpp:1795-1830).
    /// </summary>
    private const int MaxMonsterDropItemId = 99_999;

    private const int MaxMonsterDropMoneyAmount = 100_000_000;

    /// <summary>
    ///     Shared 1-based lowest legal reference index -- the floor of the SKILL (1-300) and NPC (1-500) index
    ///     ranges below, and the canonical key the lowest-index self-test (<see cref="RunLowestIndexSelfTest" />)
    ///     probes for.
    /// </summary>
    private const int MinReferenceIndex = 1;

    // Skill_CheckValidElement bounds (Server/Header/S15_MyShare.cpp:1277-1497) -- see ValidateSkills for how each
    // is used. The 300 cap is the legacy fixed-capacity SKILL table size (Server/Header/S15_MyShare.cpp:518); a
    // skill whose index would exceed it cannot exist. Name/description buffer sizes are the live non-GXCW_SV
    // definitions (Server/Header/Protocol/STRUCT.h:123-125), expressed as "buffer size minus the terminator byte".
    private const int MaxSkillIndex = 300;
    private const int MaxSkillNameLength = 24;
    private const int MaxSkillDescriptionLength = 50;
    private const int MinSkillType = 1;
    private const int MaxSkillType = 4;
    private const int MinSkillAttackType = 1;
    private const int MaxSkillAttackType = 5;
    private const int MinSkillDataNumber2D = 1;
    private const int MaxSkillDataNumber2D = 10000;
    private const int MinSkillTribeInfo1 = 1;
    private const int MaxSkillTribeInfo1 = 4;
    private const int MinSkillTribeInfo2 = 1;
    private const int MaxSkillTribeInfo2 = 10;

    /// <summary>
    ///     Legacy floor (1) on LearnSkillPoint/MaxUpgradePoint (Server/Header/S15_MyShare.cpp:1350-1357). The legacy
    ///     ceiling of 1000 on both is unreachable beneath their <c>byte</c> DTO storage (max 255), so only the floor
    ///     is a live check here -- same column-width note the companion CK_Skills_LearnSkillPoint/MaxUpgradePoint
    ///     constraints carry (Database/Migrations/032_skill_static_data_range_checks.sql).
    /// </summary>
    private const int MinSkillLearnOrUpgradePoint = 1;

    private const int MaxSkillTotalHitNumber = 10;
    private const int MaxSkillValidRadius = 1000;
    private const int MaxSkillGradeManaUse = 10000;
    private const int MaxSkillGradeStun = 100;
    private const int MaxSkillGradeFastRunSpeed = 1000;
    private const int MaxSkillGradeAttackInfo1 = 1000;
    private const int MaxSkillGradeRunTime = 10000;

    // Npc_CheckValidElement bounds (Server/Header/S15_MyShare.cpp:1836-1963) -- see ValidateNpcs. The 500 cap is the
    // legacy fixed-capacity NPC table size (Server/Header/S15_MyShare.cpp:622). Name/speech buffer sizes are the
    // unconditional STRUCT.h:206-212 definitions, again "buffer minus terminator". These bounds also live as CHECK
    // constraints (Database/Migrations/033_npc_static_data_range_checks.sql) -- reproduced here for the same
    // fail-fast-at-boot parity ValidateMonsters carries.
    private const int MaxNpcIndex = 500;
    private const int MaxNpcNameLength = 27;
    private const int MaxNpcSpeechLength = 50;
    private const int MinNpcTribe = 1;
    private const int MaxNpcTribe = 5;
    private const int MinNpcType = 1;
    private const int MaxNpcType = 17;
    private const int MinNpcDataSortNumber = 1;
    private const int MaxNpcDataSortNumber = 10000;
    private const int MinNpcSize = 1;
    private const int MaxNpcSize = 1000;
    private const int MinNpcMenuOption = 1;
    private const int MaxNpcMenuOption = 2;
    private const int MaxNpcShopItemId = 99999;
    private const int MaxNpcSkillOfferId = 300;
    private const int MaxNpcGambleCost = 100_000_000;

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
        ValidateQuests(rows.Quests, rows.QuestRewards);
        ValidateItems(rows.Items);
        ValidateMonsters(rows.Monsters, rows.MonsterDropMoney, rows.MonsterDropPotions, rows.MonsterDropCategoryRates,
            rows.MonsterDropExtraItems, rows.MonsterDropQuestItems);
        ValidateGemSockets(rows.GemSockets);
        ValidateSkills(rows.Skills, rows.SkillDescriptions, rows.SkillGrades);
        ValidateNpcs(rows.Npcs, rows.NpcMenuOptions, rows.NpcShopItems, rows.NpcSkillOffers, rows.NpcSpeeches,
            rows.NpcGambleCosts);
        RunLowestIndexSelfTest(rows);

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
            GemSocketsByTypeAndValue = BuildGemSocketTypeValueIndex(rows.GemSockets),
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
    ///     was read, matching the legacy loader's no-skip-and-continue failure contract. LEVEL was the first of
    ///     the LEVEL/ITEM/SKILL/MONSTER/NPC/QUEST/GSOCKET "a single malformed row aborts the whole dataset"
    ///     systems whose per-row rule set (<c>Level_CheckValidElement</c>) was read in full for the behavior
    ///     contract backing this method; QUEST (<see cref="ValidateQuests" />), ITEM (<see cref="ValidateItems" />),
    ///     MONSTER (<see cref="ValidateMonsters" />), GSOCKET (<see cref="ValidateGemSockets" />), SKILL
    ///     (<see cref="ValidateSkills" />), and NPC (<see cref="ValidateNpcs" />) since gained their own partial
    ///     per-row validation, each covering only the specific bounds their backing contract confirmed -- see those
    ///     methods' own remarks for what remains unreproduced in each. A coarse lowest-index self-test
    ///     (<see cref="RunLowestIndexSelfTest" />) rounds this out, mirroring the legacy boot smoke test.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/Header/S15_MyShare.cpp:373-421 (<c>Load_Level</c>, first-invalid-row abort, no
    ///     skip-and-continue) ; Server/Header/S15_MyShare.cpp:814-876 (<c>Level_CheckValidElement</c> -- index
    ///     bounds, index-equals-position+1, the ExpRangeMin/ExpRangeMax <see cref="MaxExpRangeBound" /> bounds
    ///     (lines 824-831), the strict ExpRangeMin &lt; ExpRangeMax requirement for the same row (lines
    ///     832-835), an inter-row exact-tiling check -- ExpRangeMax + 1 must equal the next row's ExpRangeMin --
    ///     against the next row for every index but the last (lines 836-842), a 0-100 bound on RangeInfo3 (lines
    ///     843-846), and the 0-10000 <see cref="MaxLevelCombatStat" /> bound applied to every combat stat plus
    ///     Life and Mana (lines 847-874)) ; Server/Header/Protocol/DEFINE.h:604 (<c>MAX_LIMIT_LEVEL_NUM</c> =
    ///     145, reproduced below as <see cref="MaxLevelIndex" />, the upper bound on
    ///     <see cref="LevelRowDto.Level" />) ; Server/Header/Protocol/DEFINE.h:365 (<c>MAX_NUMBER_SIZE</c> =
    ///     2,000,000,000, reproduced below as <see cref="MaxExpRangeBound" />).
    ///     Deliberately NOT reproduced: the legacy loader also hard-fails unless the on-disk row count equals 145
    ///     exactly (<c>ZlibScope::Unpack005Copy</c>, Server/Header/Scope/ZlibScope.h:86-107) -- that is a
    ///     fixed-size-shared-memory-array sizing artifact with no Fenrir equivalent (same reasoning as the
    ///     ZonePortals SHM parity note on <see cref="Build" /> above), so a seed with fewer than 145 contiguous
    ///     levels is accepted here as long as every row it does have is internally consistent.
    ///     Each of ITEM, QUEST, MONSTER, GSOCKET, SKILL, and NPC now has its own partial validation
    ///     (<see cref="ValidateItems" />/<see cref="ValidateQuests" />/<see cref="ValidateMonsters" />/
    ///     <see cref="ValidateGemSockets" />/<see cref="ValidateSkills" />/<see cref="ValidateNpcs" />), each
    ///     reproducing only the specific bounds its backing contract confirmed and each documenting in its own
    ///     remarks what it deliberately leaves unreproduced (e.g. the fixed-array positional-identity rule, or a
    ///     legacy bound already made unreachable by the DTO's narrower storage type).
    ///     A companion storage-level <c>CK_Levels_RangeInfo3</c> CHECK constraint
    ///     (Database/Migrations/030_levels_rangeinfo3_check_constraint.sql) mirrors the RangeInfo3 bound below
    ///     directly on world.Levels, since the column's own TINYINT type (0-255) is wider than the legacy 0-100
    ///     rule and would otherwise silently accept an out-of-range value written outside this loader (a bad
    ///     reseed or hand-edit). The other four bounds here are cross-row (tiling) or already narrower than
    ///     their column types allow without one (Level's/AttackPower's SMALLINT, Life's/Mana's/ExpRangeMin's/
    ///     ExpRangeMax's INT), so they stay C#-only validation, matching this method's existing shape.
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

            if (row.ExpRangeMin < 0 || row.ExpRangeMin > MaxExpRangeBound)
                throw new InvalidOperationException(
                    $"world.Levels row for Level={row.Level} has ExpRangeMin={row.ExpRangeMin} outside the " +
                    $"legacy 0-{MaxExpRangeBound} bound.");

            if (row.ExpRangeMax < 1 || row.ExpRangeMax > MaxExpRangeBound)
                throw new InvalidOperationException(
                    $"world.Levels row for Level={row.Level} has ExpRangeMax={row.ExpRangeMax} outside the " +
                    $"legacy 1-{MaxExpRangeBound} bound.");

            if (row.ExpRangeMax <= row.ExpRangeMin)
                throw new InvalidOperationException(
                    $"world.Levels row for Level={row.Level} has an invalid experience range " +
                    $"[{row.ExpRangeMin}, {row.ExpRangeMax}] -- ExpRangeMax must be strictly greater than " +
                    "ExpRangeMin.");

            if (index < ordered.Length - 1 && ordered[index + 1].ExpRangeMin != row.ExpRangeMax + 1)
                throw new InvalidOperationException(
                    $"world.Levels row for Level={row.Level} (ExpRangeMax={row.ExpRangeMax}) must tile exactly " +
                    $"into the next level's ExpRangeMin -- expected {row.ExpRangeMax + 1}, found " +
                    $"{ordered[index + 1].ExpRangeMin}.");

            if (row.RangeInfo3 > MaxRangeInfo3)
                throw new InvalidOperationException(
                    $"world.Levels row for Level={row.Level} has RangeInfo3={row.RangeInfo3} outside the legacy " +
                    $"0-{MaxRangeInfo3} bound.");

            if (row.AttackPower is < 0 or > MaxLevelCombatStat
                || row.DefensePower is < 0 or > MaxLevelCombatStat
                || row.AttackSuccess is < 0 or > MaxLevelCombatStat
                || row.AttackBlock is < 0 or > MaxLevelCombatStat
                || row.ElementAttack is < 0 or > MaxLevelCombatStat
                || row.Life is < 0 or > MaxLevelCombatStat
                || row.Mana is < 0 or > MaxLevelCombatStat)
                throw new InvalidOperationException(
                    $"world.Levels row for Level={row.Level} has a combat stat, Life, or Mana outside the " +
                    $"legacy 0-{MaxLevelCombatStat} bound.");
        }
    }

    /// <summary>
    ///     Per-row QUEST validation: aborts the whole load on the first invalid Step or reward Amount, matching the
    ///     legacy loader's no-skip-and-continue failure contract for this dataset. Closes the two items the Quest
    ///     static-data load-time validation contract flags as a concrete, unreplicated gap; every other legacy
    ///     <c>Quest_CheckValidElement</c> bound is already enforced either by an existing CHECK constraint on
    ///     world.Quests/world.QuestRewards (Category/Type/Sort/SlotIndex/RewardType/ItemXorAmount) or by a foreign
    ///     key (Level/SummonZoneNumber/Start-End-KeyNPCNumber/NextIndex), per that contract's own item-by-item
    ///     disposition.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/Header/S15_MyShare.cpp:2003-2007 (Step, <see cref="MinQuestStep" />-<see cref="MaxQuestStep" />
    ///     bound) ; Server/Header/S15_MyShare.cpp:2044-2054 (reward Amount, <see cref="MinQuestRewardAmount" />-
    ///     <see cref="MaxQuestRewardAmount" /> bound, nullable-aware -- Amount is unset for RewardType 6 item
    ///     rewards) ; Server/Header/S15_MyShare.cpp:670-719 (<c>Load_Quest</c>, first-invalid-record abort of the
    ///     whole 1000-slot table, no skip-and-continue) ; Server/ts25sharemem/main.cpp:87-93 (a <c>Load_Quest</c>
    ///     failure is fatal to ts25sharemem's own boot).
    ///     A companion storage-level pair of CHECK constraints (<c>CK_Quests_Step</c>, <c>CK_QuestRewards_Amount</c>,
    ///     Database/Migrations/031_quest_step_and_reward_amount_range_checks.sql) mirrors both bounds directly on
    ///     world.Quests/world.QuestRewards, since neither table enforced them before this pair was added. Unlike
    ///     Levels' RangeInfo3 case, both bounds here are already narrower than their column types (Step is
    ///     SMALLINT, Amount is INT), so the DB constraint is not compensating for a wider column -- this method
    ///     still reproduces the same fail-fast-at-boot contract in C# for parity with every other per-row-validated
    ///     dataset (LEVEL/ITEM/MONSTER/GSOCKET/SKILL/NPC today, see also <see cref="ValidateItems" />/
    ///     <see cref="ValidateMonsters" />/<see cref="ValidateGemSockets" />/<see cref="ValidateSkills" />/
    ///     <see cref="ValidateNpcs" />).
    ///     Deliberately NOT reproduced here: a 1-1000 bound on QuestId itself, and the legacy array's
    ///     positional-consistency rule (index equals array slot) -- the contract backing this method explicitly
    ///     ranks both lower severity / not applicable to a relational PRIMARY KEY, unlike Step/Amount which it
    ///     flags as a concrete gap.
    /// </remarks>
    private static void ValidateQuests(IReadOnlyList<QuestRowDto> quests, IReadOnlyList<QuestRewardRowDto> rewards)
    {
        foreach (var quest in quests.OrderBy(static quest => quest.QuestId))
            if (quest.Step is < MinQuestStep or > MaxQuestStep)
                throw new InvalidOperationException(
                    $"world.Quests row QuestId={quest.QuestId} has Step={quest.Step} outside the legacy " +
                    $"{MinQuestStep}-{MaxQuestStep} bound.");

        foreach (var reward in rewards.OrderBy(static reward => reward.QuestId)
                     .ThenBy(static reward => reward.SlotIndex))
            if (reward.Amount is { } amount && amount is < MinQuestRewardAmount or > MaxQuestRewardAmount)
                throw new InvalidOperationException(
                    $"world.QuestRewards row QuestId={reward.QuestId} SlotIndex={reward.SlotIndex} has " +
                    $"Amount={amount} outside the legacy {MinQuestRewardAmount}-{MaxQuestRewardAmount} bound.");
    }

    /// <summary>
    ///     Per-row ITEM validation: aborts the whole load on the first invalid row, matching the legacy loader's
    ///     no-skip-and-continue failure contract for this dataset. Covers only the subset of
    ///     <c>Item_CheckValidElement</c>'s rule set the Item static-data load-time validation contract confirmed
    ///     as a concrete gap -- the same three fields (CheckDateItem/DataNumber3D/AddDataNumber3D) whose column
    ///     width was too narrow to even represent their own legacy-valid range, plus the PotionType1/PotionType2
    ///     compound bound that has no per-column CHECK analogue at all. Every other <c>Item_CheckValidElement</c>
    ///     field bound (Type, Sort, PotionType1's own range, MartialLevel, EquipInfo1/EquipInfo2, the eleven
    ///     <c>Check*</c> flags, every stat field, Critical, LastAttackBonusInfo1/2, CapeInfo1-3) remains
    ///     unread/unverified for this method and is deliberately NOT reproduced here -- guessing at those bounds
    ///     from the field names alone would violate the no-legacy-parity-from-memory rule; closing them remains
    ///     an open gap for a follow-up <c>legacy-behavior-translator</c> contract, same as SKILL/NPC/GSOCKET per
    ///     <see cref="ValidateLevels" />'s own remarks. GainSkillNumber needs no C# check here: it is
    ///     already referentially enforced by <c>FK_Items_Skills_GainSkillNumber</c> (Database/Tables/world/
    ///     Items.sql), a plain FK rather than a numeric bound.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/Header/S15_MyShare.cpp:880-888 (<c>MAX_ITEM_STATUS_ATTRIBUTE</c> = 10000, reproduced
    ///     below as <see cref="MaxItemDataNumber3D" />) ; Server/Header/S15_MyShare.cpp:889-919
    ///     (<c>Item_CheckValidElement</c>, per ServerDocs/19_Header_Lib/12_Divers_DonneesJeu.md:618 ; the same doc's
    ///     line 231 notes iIndex == 0 is treated as a valid empty item slot rather than a row to validate, per
    ///     Server/Header/S15_MyShare.cpp:893-896 -- reproduced below as the <c>ItemId == 0</c> skip) ;
    ///     Server/Header/S15_MyShare.cpp:950-959 (DataNumber3D/AddDataNumber3D, the <see cref="MaxItemDataNumber3D" />
    ///     bound) ; Server/Header/S15_MyShare.cpp:1065-1069 (CheckDateItem, the <see cref="MaxItemCheckDateItem" />
    ///     bound) ; Server/Header/S15_MyShare.cpp:1130-1147 (PotionType2's conditional bound on PotionType1 --
    ///     <see cref="MinPotionType2Restricted" />-<see cref="MaxPotionType2Restricted" /> when PotionType1 equals
    ///     <see cref="PotionType1RestrictedValue" />, else 0-<see cref="MaxPotionType2Default" />).
    ///     A companion set of storage-level CHECK constraints (<c>CK_Items_CheckDateItem</c>,
    ///     <c>CK_Items_DataNumber3D</c>, <c>CK_Items_AddDataNumber3D</c>, <c>CK_Items_PotionType2Range</c>,
    ///     Database/Migrations/038_items_column_width_and_potiontype_checks.sql) mirrors all four bounds directly
    ///     on world.Items -- the first three because CheckDateItem/DataNumber3D/AddDataNumber3D were widened from
    ///     TINYINT to SMALLINT by that same migration (TINYINT's 0-255 range could never hold a legacy-valid
    ///     366-vs-365 or 256-vs-10000 value), same reasoning as Levels' RangeInfo3 case ; the fourth because a
    ///     single-column CHECK cannot express a bound conditional on a sibling column, but a same-row multi-column
    ///     CHECK can. This method still reproduces the same fail-fast-at-boot contract in C# for parity with every
    ///     other per-row-validated dataset, matching <see cref="ValidateLevels" />/<see cref="ValidateQuests" />'s
    ///     own shape.
    /// </remarks>
    private static void ValidateItems(IReadOnlyList<ItemRowDto> items)
    {
        foreach (var item in items.OrderBy(static item => item.ItemId))
        {
            // ItemId == 0 marks an empty/nonexistent item slot in the legacy data, not a real record -- see
            // this method's own remarks for the citation. world.Items never actually seeds such a row today, but
            // the skip is kept for parity should one ever appear.
            if (item.ItemId == 0)
                continue;

            if (item.CheckDateItem is < 0 or > MaxItemCheckDateItem)
                throw new InvalidOperationException(
                    $"world.Items row ItemId={item.ItemId} has CheckDateItem={item.CheckDateItem} outside the " +
                    $"legacy 0-{MaxItemCheckDateItem} bound.");

            if (item.DataNumber3D is < 0 or > MaxItemDataNumber3D)
                throw new InvalidOperationException(
                    $"world.Items row ItemId={item.ItemId} has DataNumber3D={item.DataNumber3D} outside the " +
                    $"legacy 0-{MaxItemDataNumber3D} bound.");

            if (item.AddDataNumber3D is < 0 or > MaxItemDataNumber3D)
                throw new InvalidOperationException(
                    $"world.Items row ItemId={item.ItemId} has AddDataNumber3D={item.AddDataNumber3D} outside " +
                    $"the legacy 0-{MaxItemDataNumber3D} bound.");

            var potionType2InRange = item.PotionType1 == PotionType1RestrictedValue
                ? item.PotionType2 is >= MinPotionType2Restricted and <= MaxPotionType2Restricted
                : item.PotionType2 is >= 0 and <= MaxPotionType2Default;

            if (!potionType2InRange)
                throw new InvalidOperationException(
                    $"world.Items row ItemId={item.ItemId} has PotionType2={item.PotionType2} outside the " +
                    $"legacy bound for PotionType1={item.PotionType1} (" +
                    $"{MinPotionType2Restricted}-{MaxPotionType2Restricted} when PotionType1=" +
                    $"{PotionType1RestrictedValue}, else 0-{MaxPotionType2Default}).");
        }
    }

    /// <summary>
    ///     Per-row GSOCKET validation: aborts the whole load on the first invalid row, matching the legacy
    ///     loader's no-skip-and-continue failure contract for this dataset. Closes the Gem socket static-data
    ///     load-time validation contract's core finding -- none of the zero-bypass rules, the Type 1-46 range
    ///     gate, or the five Type-band rules were enforced anywhere before this method and its companion CHECK
    ///     constraint were added; GSOCKET previously had zero per-row validation, unlike ITEM/QUEST which
    ///     already had their own partial coverage (see <see cref="ValidateItems" />/<see cref="ValidateQuests" />).
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/Header/S15_MyShare.cpp:2212-2268 (<c>GSocket_CheckValidElement</c>, evaluated in
    ///     this exact order) -- Value02 = 0 unconditionally accepts ahead of every other rule (:2214-2215) ;
    ///     Type = 0 (with Value02 &lt;&gt; 0) likewise unconditionally accepts (:2216-2217) ; Type outside
    ///     <see cref="MinGemSocketType" />-<see cref="MaxGemSocketType" /> rejects (:2218) ; Type = 1 requires
    ///     Value02 1-33, Value03 0-400, Value04 0-400 (:2222-2230) ; Type 2-29 requires Value02 1-100, Value03
    ///     0-1000, Value04 0-1000 (:2231-2239) ; Type 39-42 requires Value02 1-10, Value03 &gt;= 1, Value04 = 0
    ///     (:2249-2257) ; Type 43-46 requires Value02 1-10, Value03 &gt;= 6, Value04 = 0 (:2258-2266) ;
    ///     Server/Header/S15_MyShare.cpp:749-756 (<c>Load_GSocket</c>, first-invalid-record abort of the whole
    ///     2891-slot table, no skip-and-continue).
    ///     Deliberately NOT reproduced: Value01's stated range check (:2220) is dead code -- it requires Value01
    ///     to be simultaneously negative and &gt; 10000, which no integer satisfies, so it never rejects any
    ///     record. The Type 30-38 band (:2240-2248) is also dead code -- it requires Type to be simultaneously
    ///     &gt;= 30 and &lt;= 28 -- so every record in that window falls through unconstrained to the final
    ///     unconditional accept (:2267); the true intended bounds cannot be recovered from the source as
    ///     written, so this method (like its companion CHECK constraint) leaves that band unconstrained rather
    ///     than guess at bounds that cannot be cited.
    ///     A companion storage-level <c>CK_GemSockets_TypeBandRules</c> CHECK constraint
    ///     (Database/Migrations/034_gem_socket_type_band_range_checks.sql) mirrors every bound above directly on
    ///     world.GemSockets, since none of them were enforced by the table's schema before that migration. This
    ///     method still reproduces the same fail-fast-at-boot contract in C# for parity with every other
    ///     per-row-validated dataset (LEVEL/QUEST/ITEM/SKILL/NPC above, see also <see cref="ValidateSkills" />/
    ///     <see cref="ValidateNpcs" />).
    /// </remarks>
    private static void ValidateGemSockets(IReadOnlyList<GemSocketRowDto> gemSockets)
    {
        foreach (var gemSocket in gemSockets.OrderBy(static gemSocket => gemSocket.GemSocketId))
        {
            // Value02 = 0, or Type = 0, unconditionally accepts ahead of every band rule below -- see this
            // method's own remarks for the citation.
            if (gemSocket.Value02 == 0 || gemSocket.Type == 0)
                continue;

            // Every arm below corresponds to one legacy Type band; an unmatched Type (outside 1-46, or a
            // negative value) falls to the default arm and is rejected, reproducing the legacy Type
            // 1-46 range gate without a separate check.
            var satisfiesBand = gemSocket.Type switch
            {
                1 => gemSocket.Value02 is >= 1 and <= 33
                     && gemSocket.Value03 is >= 0 and <= 400
                     && gemSocket.Value04 is >= 0 and <= 400,
                >= 2 and <= 29 => gemSocket.Value02 is >= 1 and <= 100
                                  && gemSocket.Value03 is >= 0 and <= 1000
                                  && gemSocket.Value04 is >= 0 and <= 1000,
                // Dead band in the legacy source (its own guard can never fire, see this method's remarks) --
                // deliberately left unconstrained here to match legacy's actual runtime behavior.
                >= 30 and <= 38 => true,
                >= 39 and <= 42 => gemSocket.Value02 is >= 1 and <= 10
                                   && gemSocket.Value03 >= 1
                                   && gemSocket.Value04 == 0,
                >= 43 and <= 46 => gemSocket.Value02 is >= 1 and <= 10
                                   && gemSocket.Value03 >= 6
                                   && gemSocket.Value04 == 0,
                _ => false
            };

            if (!satisfiesBand)
                throw new InvalidOperationException(
                    $"world.GemSockets row GemSocketId={gemSocket.GemSocketId} has Type={gemSocket.Type}, " +
                    $"Value02={gemSocket.Value02}, Value03={gemSocket.Value03}, Value04={gemSocket.Value04} " +
                    $"violating the legacy Type-band rules (Type must be {MinGemSocketType}-{MaxGemSocketType} " +
                    "with band-specific Value02/Value03/Value04 bounds).");
        }
    }

    /// <summary>
    ///     Builds <see cref="WorldDataCache.GemSocketsByTypeAndValue" /> from the same rows
    ///     <see cref="ValidateGemSockets" /> already validated -- keyed by (Type, Value02), first-GemSocketId-wins
    ///     on a duplicate key, reproducing <c>GSOCKET::Search</c>'s first-match linear scan
    ///     (Server/ts25zone/GameSystem/GameSystem_08_Socket.cpp:22-32: reject a query whose type or value is below
    ///     1 -- enforced by the reader, <c>StatCalculator.ResolveGemSocketValue</c>, not by this index -- else
    ///     return the first row matching both fields).
    /// </summary>
    /// <remarks>
    ///     Workstream B13-socket-prerequisites: the resolver-side routing/decode logic
    ///     (<c>Fenrir.Application.Game.Stats.StatCalculator.GemSocketContribution.cs</c>) already existed and
    ///     needed exactly this shape of table, keyed with
    ///     <c>StatCalculator.GemSocketTypeValueKey(byte gemType, byte gemValue)</c> =
    ///     <c>(gemType &lt;&lt; 8) | gemValue</c>. That formula is duplicated in <see cref="GemSocketTypeValueKey" />
    ///     below rather than called directly because <c>Fenrir.Application.Game.GameData</c> has no project
    ///     reference to <c>Fenrir.Application.Game.Stats</c> (siblings under Application/, Stats depends on
    ///     neither GameData nor the reverse) -- the same "leaf project can't share a tiny formula across an
    ///     un-referenced sibling" situation already accepted for the valid-costume set duplicated in
    ///     <c>NpcShopPolicy</c>. Both formulas must be kept in sync if either changes; there is no compiler
    ///     enforcement of that today.
    /// </remarks>
    private static FrozenDictionary<int, GemSocketRowDto> BuildGemSocketTypeValueIndex(
        IReadOnlyList<GemSocketRowDto> gemSockets)
    {
        var byTypeAndValue = new Dictionary<int, GemSocketRowDto>(gemSockets.Count);
        foreach (var gemSocket in gemSockets.OrderBy(static gemSocket => gemSocket.GemSocketId))
            byTypeAndValue.TryAdd(GemSocketTypeValueKey(gemSocket.Type, gemSocket.Value02), gemSocket);

        return byTypeAndValue.ToFrozenDictionary();
    }

    /// <summary>
    ///     MUST stay byte-for-byte identical to
    ///     <c>Fenrir.Application.Game.Stats.StatCalculator.GemSocketTypeValueKey(byte, byte)</c> -- see
    ///     <see cref="BuildGemSocketTypeValueIndex" />'s own remarks for why this project cannot simply call that
    ///     one. Every <see cref="GemSocketRowDto.Type" />/<see cref="GemSocketRowDto.Value02" /> that reaches this
    ///     method has already passed <see cref="ValidateGemSockets" /> (Type is 0 or <see cref="MinGemSocketType" />-
    ///     <see cref="MaxGemSocketType" />; Value02 is band-bounded, at most 100, whenever Type is nonzero and
    ///     Value02 is nonzero) -- both operands fit a byte in every case this index is ever actually queried for
    ///     (a query with type or value below 1 is rejected by the reader before any lookup, so an out-of-band
    ///     Type=0/Value02=0 escape row masking to an unexpected key is harmless).
    /// </summary>
    private static int GemSocketTypeValueKey(int type, int value02)
    {
        return ((type & 0xFF) << 8) | (value02 & 0xFF);
    }

    /// <summary>
    ///     Per-row MONSTER validation: aborts the whole load on the first invalid row across world.Monsters and all
    ///     five MonsterDrop* child tables, matching the legacy loader's no-skip-and-continue failure contract for
    ///     this dataset. Reproduces every single-field bound and the three cross-field-ordering rules from
    ///     <c>Monster_CheckValidElement</c> that the companion storage-level migration also expresses as row-scoped
    ///     SQL CHECK constraints -- see this method's own remarks for why both layers carry the same rules.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/Header/S15_MyShare.cpp:1500-1833 (<c>MyShm::Monster_CheckValidElement</c> -- every
    ///     bound below, field by field) ; Server/Header/S15_MyShare.cpp:565-605 (<c>MyShm::Load_Monster</c>,
    ///     first-offending-slot abort, no skip-and-continue) ; Server/ts25sharemem/main.cpp:73-79 ;
    ///     Server/ts25zone/GameSystem/GameSystem_04_Monster.cpp:10-19 -&gt;
    ///     Server/ts25zone/GameSystem/GameSystem_00_Load.cpp:37-41 -&gt;
    ///     Server/ts25zone/S01_MainApplication.cpp:277-282 (a <c>Load_Monster</c> failure is fatal to whichever
    ///     process performed the load).
    ///     A companion set of storage-level CHECK constraints (25 grouped constraints on world.Monsters, 4 on
    ///     world.MonsterAnimationFrames -- whose columns live on <see cref="MonsterRowDto" /> here, not a separate
    ///     DTO, since world.usp_Monster_GetAll already joins them -- and value-range additions on all five
    ///     MonsterDrop* child tables, Database/Migrations/036_monster_static_data_range_checks.sql) mirrors every
    ///     bound below directly on the tables themselves. This method still reproduces the same fail-fast-at-boot
    ///     contract in C# for parity with every other per-row-validated dataset (LEVEL/QUEST/ITEM/GSOCKET, see
    ///     <see cref="ValidateLevels" />/<see cref="ValidateQuests" />/<see cref="ValidateItems" />/
    ///     <see cref="ValidateGemSockets" />), and to catch a bad row that predates the constraints or reached the
    ///     table by some path the constraints don't cover.
    ///     Deliberately NOT reproduced here, matching that migration's own disposition of both items:
    ///     <list type="bullet">
    ///         <item>
    ///             MonsterId "must equal its own array slot position" (slot index + 1), including the
    ///             zero-slot-is-skipped edge case -- MonsterId is Fenrir's PRIMARY KEY, boot-time/generator-assigned
    ///             reference data, not externally supplied input, same reasoning <see cref="ValidateQuests" />
    ///             applies to QuestId.
    ///         </item>
    ///         <item>
    ///             The Thunder Giant (MonsterId 81) unconditional AttackType=1 load-time patch
    ///             (Server/Header/S15_MyShare.cpp:603-604) -- a load-time value patch has no validation-rule
    ///             equivalent; it is a data-import-tooling concern, not something a per-row check can express.
    ///         </item>
    ///     </list>
    ///     Name/ChatLine1/ChatLine2 translation note: legacy's "must contain a null terminator within its N-byte
    ///     buffer" requirement becomes, for a real string with no fixed-buffer/null-terminator concept, "stored
    ///     content must be at most N-1 characters" (<see cref="MaxMonsterNameLength" />/
    ///     <see cref="MaxMonsterChatLineLength" /> are already the buffer size minus one), matching the migration's
    ///     own translation note.
    /// </remarks>
    private static void ValidateMonsters(
        IReadOnlyList<MonsterRowDto> monsters,
        IReadOnlyList<MonsterDropMoneyRowDto> dropMoney,
        IReadOnlyList<MonsterDropPotionRowDto> dropPotions,
        IReadOnlyList<MonsterDropCategoryRateRowDto> dropCategoryRates,
        IReadOnlyList<MonsterDropExtraItemRowDto> dropExtraItems,
        IReadOnlyList<MonsterDropQuestItemRowDto> dropQuestItems)
    {
        foreach (var monster in monsters.OrderBy(static monster => monster.MonsterId))
        {
            if (monster.Name.Length > MaxMonsterNameLength)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a Name longer than " +
                    $"{MaxMonsterNameLength} characters.");

            if (monster.ChatLine1 is { Length: > MaxMonsterChatLineLength }
                || monster.ChatLine2 is { Length: > MaxMonsterChatLineLength })
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a ChatLine longer than " +
                    $"{MaxMonsterChatLineLength} characters.");

            if (monster.Type is < MinMonsterType or > MaxMonsterType)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has Type={monster.Type} outside the " +
                    $"legacy {MinMonsterType}-{MaxMonsterType} bound.");

            if (monster.SpecialType is < MinMonsterSpecialType or > MaxMonsterSpecialType)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has SpecialType={monster.SpecialType} " +
                    $"outside the legacy {MinMonsterSpecialType}-{MaxMonsterSpecialType} bound.");

            if (monster.DamageType is < MinMonsterDamageType or > MaxMonsterDamageType)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has DamageType={monster.DamageType} " +
                    $"outside the legacy {MinMonsterDamageType}-{MaxMonsterDamageType} bound.");

            if (monster.DataSortNumber is < MinMonsterDataSortNumber or > MaxMonsterDataSortNumber)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has DataSortNumber=" +
                    $"{monster.DataSortNumber} outside the legacy {MinMonsterDataSortNumber}-" +
                    $"{MaxMonsterDataSortNumber} bound.");

            if (monster.Size1 is < MinMonsterSize or > MaxMonsterSize
                || monster.Size2 is < MinMonsterSize or > MaxMonsterSize
                || monster.Size3 is < MinMonsterSize or > MaxMonsterSize)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a Size1/Size2/Size3 outside the " +
                    $"legacy {MinMonsterSize}-{MaxMonsterSize} bound.");

            if (monster.Size4 is < 0 or > MaxMonsterSize4)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has Size4={monster.Size4} outside the " +
                    $"legacy 0-{MaxMonsterSize4} bound.");

            if (monster.SizeCategory is < MinMonsterSizeCategory or > MaxMonsterSizeCategory)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has SizeCategory={monster.SizeCategory} " +
                    $"outside the legacy {MinMonsterSizeCategory}-{MaxMonsterSizeCategory} bound.");

            if (monster.CheckCollision is < MinMonsterCheckCollision or > MaxMonsterCheckCollision)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has CheckCollision=" +
                    $"{monster.CheckCollision} outside the legacy {MinMonsterCheckCollision}-" +
                    $"{MaxMonsterCheckCollision} bound.");

            if (monster.TotalHitNum > MaxMonsterHitCount || monster.TotalSkillHitNum > MaxMonsterHitCount)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a TotalHitNum/TotalSkillHitNum " +
                    $"outside the legacy 0-{MaxMonsterHitCount} bound.");

            if (monster.ItemLevel is < MinMonsterItemLevel or > MaxMonsterItemLevel)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has ItemLevel={monster.ItemLevel} " +
                    $"outside the legacy {MinMonsterItemLevel}-{MaxMonsterItemLevel} bound.");

            if (monster.MartialItemLevel is < 0 or > MaxMonsterMartialItemLevel)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has MartialItemLevel=" +
                    $"{monster.MartialItemLevel} outside the legacy 0-{MaxMonsterMartialItemLevel} bound.");

            if (monster.RealLevel is < MinMonsterRealLevel or > MaxMonsterRealLevel)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has RealLevel={monster.RealLevel} " +
                    $"outside the legacy {MinMonsterRealLevel}-{MaxMonsterRealLevel} bound.");

            if (monster.MartialRealLevel is < 0 or > MaxMonsterMartialRealLevel)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has MartialRealLevel=" +
                    $"{monster.MartialRealLevel} outside the legacy 0-{MaxMonsterMartialRealLevel} bound.");

            if (monster.GeneralExperience is < 0 or > MaxMonsterExperienceReward
                || monster.PatExperience is < 0 or > MaxMonsterExperienceReward)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a GeneralExperience/PatExperience " +
                    $"outside the legacy 0-{MaxMonsterExperienceReward} bound.");

            if (monster.Life is < MinMonsterLife or > MaxMonsterLife)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has Life={monster.Life} outside the " +
                    $"legacy {MinMonsterLife}-{MaxMonsterLife} bound.");

            if (monster.AttackType is < MinMonsterAttackType or > MaxMonsterAttackType)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has AttackType={monster.AttackType} " +
                    $"outside the legacy {MinMonsterAttackType}-{MaxMonsterAttackType} bound.");

            if (monster.RadiusInfo1 is < 0 or > MaxMonsterRadiusInfo
                || monster.RadiusInfo2 is < 0 or > MaxMonsterRadiusInfo
                || monster.RadiusInfo2 < monster.RadiusInfo1)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has an invalid RadiusInfo1/RadiusInfo2 " +
                    $"pair [{monster.RadiusInfo1}, {monster.RadiusInfo2}] -- both must be 0-" +
                    $"{MaxMonsterRadiusInfo} and RadiusInfo2 must be greater than or equal to RadiusInfo1.");

            if (monster.WalkSpeed is < 0 or > MaxMonsterMovementSpeed
                || monster.RunSpeed is < 0 or > MaxMonsterMovementSpeed
                || monster.DeathSpeed is < 0 or > MaxMonsterMovementSpeed)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a WalkSpeed/RunSpeed/DeathSpeed " +
                    $"outside the legacy 0-{MaxMonsterMovementSpeed} bound.");

            if (monster.AttackPower is < 0 or > MaxMonsterAttackPower)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has AttackPower={monster.AttackPower} " +
                    $"outside the legacy 0-{MaxMonsterAttackPower} bound.");

            if (monster.DefensePower is < 0 or > MaxMonsterCombatStat
                || monster.AttackSuccess is < 0 or > MaxMonsterCombatStat
                || monster.AttackBlock is < 0 or > MaxMonsterCombatStat
                || monster.ElementAttackPower is < 0 or > MaxMonsterCombatStat
                || monster.ElementDefensePower is < 0 or > MaxMonsterCombatStat)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a DefensePower/AttackSuccess/" +
                    $"AttackBlock/ElementAttackPower/ElementDefensePower outside the legacy 0-" +
                    $"{MaxMonsterCombatStat} bound.");

            if (monster.Critical is < 0 or > MaxMonsterCritical)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has Critical={monster.Critical} " +
                    $"outside the legacy 0-{MaxMonsterCritical} bound.");

            if (monster.FollowInfo1 is < 0 or > MaxMonsterFollowInfo
                || monster.FollowInfo2 is < 0 or > MaxMonsterFollowInfo
                || monster.FollowInfo2 < monster.FollowInfo1)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has an invalid FollowInfo1/FollowInfo2 " +
                    $"pair [{monster.FollowInfo1}, {monster.FollowInfo2}] -- both must be 0-" +
                    $"{MaxMonsterFollowInfo} and FollowInfo2 must be greater than or equal to FollowInfo1.");

            if (monster.SummonTime1 is < MinMonsterSummonTime or > MaxMonsterSummonTime
                || monster.SummonTime2 is < MinMonsterSummonTime or > MaxMonsterSummonTime
                || monster.SummonTime2 < monster.SummonTime1)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has an invalid SummonTime1/SummonTime2 " +
                    $"pair [{monster.SummonTime1}, {monster.SummonTime2}] -- both must be " +
                    $"{MinMonsterSummonTime}-{MaxMonsterSummonTime} and SummonTime2 must be greater than or " +
                    "equal to SummonTime1.");

            if (monster.FrameInfo1 is < MinMonsterFrameInfo or > MaxMonsterFrameInfo
                || monster.FrameInfo2 is < MinMonsterFrameInfo or > MaxMonsterFrameInfo
                || monster.FrameInfo3 is < MinMonsterFrameInfo or > MaxMonsterFrameInfo
                || monster.FrameInfo4 is < MinMonsterFrameInfo or > MaxMonsterFrameInfo
                || monster.FrameInfo5 is < MinMonsterFrameInfo or > MaxMonsterFrameInfo
                || monster.FrameInfo6 is < MinMonsterFrameInfo or > MaxMonsterFrameInfo)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a FrameInfo1-6 outside the legacy " +
                    $"{MinMonsterFrameInfo}-{MaxMonsterFrameInfo} bound.");

            if (monster.HitFrame1 is < 0 or > MaxMonsterHitFrame
                || monster.HitFrame2 is < 0 or > MaxMonsterHitFrame
                || monster.HitFrame3 is < 0 or > MaxMonsterHitFrame)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a HitFrame1-3 outside the legacy " +
                    $"0-{MaxMonsterHitFrame} bound.");

            if (monster.SkillHitFrame1 is < 0 or > MaxMonsterHitFrame
                || monster.SkillHitFrame2 is < 0 or > MaxMonsterHitFrame
                || monster.SkillHitFrame3 is < 0 or > MaxMonsterHitFrame)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a SkillHitFrame1-3 outside the " +
                    $"legacy 0-{MaxMonsterHitFrame} bound.");

            if (monster.BulletInfo1 is < MinMonsterFrameInfo or > MaxMonsterFrameInfo
                || monster.BulletInfo2 is < MinMonsterFrameInfo or > MaxMonsterFrameInfo)
                throw new InvalidOperationException(
                    $"world.Monsters row MonsterId={monster.MonsterId} has a BulletInfo1/BulletInfo2 outside " +
                    $"the legacy {MinMonsterFrameInfo}-{MaxMonsterFrameInfo} bound.");
        }

        foreach (var row in dropMoney.OrderBy(static row => row.MonsterId))
            if (row.DropRate is < 0 or > MaxMonsterDropRate
                || row.MinAmount is < 0 or > MaxMonsterDropMoneyAmount
                || row.MaxAmount is < 0 or > MaxMonsterDropMoneyAmount)
                throw new InvalidOperationException(
                    $"world.MonsterDropMoney row MonsterId={row.MonsterId} has a DropRate/MinAmount/MaxAmount " +
                    "outside its legacy bound (DropRate 0-" +
                    $"{MaxMonsterDropRate}, MinAmount/MaxAmount 0-{MaxMonsterDropMoneyAmount}).");

        foreach (var row in dropPotions.OrderBy(static row => row.MonsterId).ThenBy(static row => row.SlotIndex))
            if (row.DropRate is < 0 or > MaxMonsterDropRate || row.PotionItemId is < 0 or > MaxMonsterDropItemId)
                throw new InvalidOperationException(
                    $"world.MonsterDropPotions row MonsterId={row.MonsterId} SlotIndex={row.SlotIndex} has a " +
                    $"DropRate/PotionItemId outside its legacy bound (DropRate 0-{MaxMonsterDropRate}, " +
                    $"PotionItemId 0-{MaxMonsterDropItemId}).");

        foreach (var row in dropCategoryRates.OrderBy(static row => row.MonsterId)
                     .ThenBy(static row => row.CategoryIndex))
            if (row.Value is < 0 or > MaxMonsterDropRate)
                throw new InvalidOperationException(
                    $"world.MonsterDropCategoryRates row MonsterId={row.MonsterId} " +
                    $"CategoryIndex={row.CategoryIndex} has Value={row.Value} outside the legacy 0-" +
                    $"{MaxMonsterDropRate} bound.");

        foreach (var row in dropExtraItems.OrderBy(static row => row.MonsterId).ThenBy(static row => row.SlotIndex))
            if (row.DropRate is < 0 or > MaxMonsterDropRate
                || (row.ItemId is { } itemId && itemId is < 0 or > MaxMonsterDropItemId))
                throw new InvalidOperationException(
                    $"world.MonsterDropExtraItems row MonsterId={row.MonsterId} SlotIndex={row.SlotIndex} has " +
                    $"a DropRate/ItemId outside its legacy bound (DropRate 0-{MaxMonsterDropRate}, ItemId 0-" +
                    $"{MaxMonsterDropItemId}).");

        foreach (var row in dropQuestItems.OrderBy(static row => row.MonsterId))
            if (row.DropRate is < 0 or > MaxMonsterDropRate || row.QuestItemId is < 0 or > MaxMonsterDropItemId)
                throw new InvalidOperationException(
                    $"world.MonsterDropQuestItems row MonsterId={row.MonsterId} has a DropRate/QuestItemId " +
                    $"outside its legacy bound (DropRate 0-{MaxMonsterDropRate}, QuestItemId 0-" +
                    $"{MaxMonsterDropItemId}).");
    }

    /// <summary>
    ///     Per-row SKILL validation across world.Skills and its two child tables (world.SkillDescriptions,
    ///     world.SkillGrades): aborts the whole load on the first invalid row, matching the legacy loader's
    ///     no-skip-and-continue failure contract. Reproduces the 300-slot capacity cap and every
    ///     <c>Skill_CheckValidElement</c> single-field bound that is actually representable-and-violable given the
    ///     relational DTO's column types -- see this method's own remarks for what is deliberately left out and why.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/Header/S15_MyShare.cpp:1277-1497 (<c>MyShm::Skill_CheckValidElement</c> -- empty-row
    ///     sentinel, 1-300 index bound, name/description termination, every scalar bound, and the 2 grade rows'
    ///     per-field bounds) ; Server/Header/S15_MyShare.cpp:515-563 (<c>Load_Skill</c> -- the 300-row fixed
    ///     capacity at :518, the full-capacity validation scan at :544 iterating the fixed cap rather than the
    ///     loaded count, first-invalid logs the row index and aborts at :546-550) ;
    ///     Server/Header/Protocol/STRUCT.h:123-125 (the live non-<c>GXCW_SV</c> name 25-byte / description 10×51-byte
    ///     buffer definitions -- <c>GXCW_SV</c> is never <c>#define</c>d anywhere under Server/, only two
    ///     <c>#ifndef GXCW_SV</c> guards at STRUCT.h:122,218).
    ///     A companion set of storage-level CHECK constraints
    ///     (Database/Migrations/032_skill_static_data_range_checks.sql) already enforces every one of these numeric
    ///     bounds directly on world.Skills/world.SkillGrades; this method reproduces the same fail-fast-at-boot
    ///     contract in C# for parity with every other per-row-validated dataset and to catch a bad row that predates
    ///     those constraints or reached the tables by a path they don't cover, matching <see cref="ValidateMonsters" />'s
    ///     own defense-in-depth reasoning.
    ///     <para>
    ///         Deliberately NOT reproduced here, each for a cited reason:
    ///         <list type="bullet">
    ///             <item>
    ///                 The legacy "index must equal its own array slot position + 1" dense-positional-identity rule
    ///                 (Server/Header/S15_MyShare.cpp:1291-1295) -- SkillId is Fenrir's relational PRIMARY KEY /
    ///                 NOT NULL, boot-time/generator-assigned reference data, so this fixed-array-storage artifact
    ///                 has no analogue once represented relationally. Same disposition
    ///                 Database/Migrations/032_skill_static_data_range_checks.sql took, and the same one
    ///                 <see cref="ValidateMonsters" />/<see cref="ValidateQuests" /> take for MonsterId/QuestId. The
    ///                 1-300 <em>range</em> cap IS reproduced (a distinct rule -- it is the fixed-capacity bound, not
    ///                 positional identity).
    ///             </item>
    ///             <item>
    ///                 Every <c>Skill_CheckValidElement</c> bound whose field is a <c>byte</c> in the DTO with a
    ///                 legacy ceiling above 255 -- LearnSkillPoint/MaxUpgradePoint (ceiling 1000, only their floor of
    ///                 1 is checked), the two RecoverInfo values (0-10000), AttackInfo2/AttackInfo3 (0-1000), and the
    ///                 ~18 grade "up" fields plus ReturnSuccessUp/DestroySuccessUp (0-1000 or 0-10000). A
    ///                 <c>byte</c>'s 0-255 storage range already sits inside every one of those ceilings, so the
    ///                 upper check can never fire and the lower check (0) is vacuous -- reproducing them would be
    ///                 dead comparisons (and, written as a relational pattern against a wider constant, a compiler
    ///                 warning under this repo's warnings-as-errors). Only ManaUse/FastRunSpeed/AttackInfo1/RunTime
    ///                 (<c>short</c>) and StunAttack/StunDefense (<c>byte</c>, ceiling 100, within range) can
    ///                 actually be violated, so only those are checked -- the same column-width note
    ///                 Database/Migrations/032 carries.
    ///             </item>
    ///             <item>
    ///                 The exactly-two-grade-rows-per-skill requirement -- a fixed-array-shape artifact; the
    ///                 composite PK plus CK_SkillGrades_GradeIndex already bound each skill to at most one grade-0
    ///                 and one grade-1 row, and this method validates whichever grade rows are present rather than
    ///                 mandating a count.
    ///             </item>
    ///         </list>
    ///     </para>
    ///     Name/description translation note (same as <see cref="ValidateMonsters" />): legacy's "must contain a
    ///     terminator within its N-byte buffer" becomes, for a real string, "content is at most N-1 characters"
    ///     (<see cref="MaxSkillNameLength" /> = 24 for the 25-byte buffer, <see cref="MaxSkillDescriptionLength" /> =
    ///     50 for the 51-byte one). This is safe against the imported seed by construction: every seeded name came
    ///     from legacy-validated data whose byte length was already within the buffer, and a UTF-16 string's
    ///     character count never exceeds that byte length.
    /// </remarks>
    private static void ValidateSkills(
        IReadOnlyList<SkillRowDto> skills,
        IReadOnlyList<SkillDescriptionRowDto> descriptions,
        IReadOnlyList<SkillGradeRowDto> grades)
    {
        foreach (var skill in skills.OrderBy(static skill => skill.SkillId))
        {
            // SkillId == 0 marks an empty/unused slot in the legacy fixed 300-slot table -- accepted (skipped),
            // never a real record. world.Skills never seeds one (SkillId is PK/NOT NULL), but the skip is kept for
            // parity, the same shape as ValidateItems' ItemId == 0 skip.
            if (skill.SkillId == 0)
                continue;

            // The 300-slot capacity cap: a skill whose index would exceed the fixed 300 capacity cannot exist.
            if (skill.SkillId is < MinReferenceIndex or > MaxSkillIndex)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} is outside the legacy {MinReferenceIndex}-" +
                    $"{MaxSkillIndex} index cap (the fixed skill-table capacity).");

            if (skill.Name.Length > MaxSkillNameLength)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has a Name longer than {MaxSkillNameLength} " +
                    "characters.");

            if (skill.Type is < MinSkillType or > MaxSkillType)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has Type={skill.Type} outside the legacy " +
                    $"{MinSkillType}-{MaxSkillType} bound.");

            if (skill.AttackType is < MinSkillAttackType or > MaxSkillAttackType)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has AttackType={skill.AttackType} outside the " +
                    $"legacy {MinSkillAttackType}-{MaxSkillAttackType} bound.");

            if (skill.DataNumber2D is < MinSkillDataNumber2D or > MaxSkillDataNumber2D)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has DataNumber2D={skill.DataNumber2D} outside the " +
                    $"legacy {MinSkillDataNumber2D}-{MaxSkillDataNumber2D} bound.");

            if (skill.TribeInfo1 is < MinSkillTribeInfo1 or > MaxSkillTribeInfo1)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has TribeInfo1={skill.TribeInfo1} outside the " +
                    $"legacy {MinSkillTribeInfo1}-{MaxSkillTribeInfo1} bound.");

            if (skill.TribeInfo2 is < MinSkillTribeInfo2 or > MaxSkillTribeInfo2)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has TribeInfo2={skill.TribeInfo2} outside the " +
                    $"legacy {MinSkillTribeInfo2}-{MaxSkillTribeInfo2} bound.");

            // LearnSkillPoint/MaxUpgradePoint: only the floor of 1 is a live check -- the legacy 1000 ceiling is
            // unreachable beneath the byte DTO column (see this method's remarks).
            if (skill.LearnSkillPoint < MinSkillLearnOrUpgradePoint)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has LearnSkillPoint={skill.LearnSkillPoint} below " +
                    $"the legacy floor of {MinSkillLearnOrUpgradePoint}.");

            if (skill.MaxUpgradePoint < MinSkillLearnOrUpgradePoint)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has MaxUpgradePoint={skill.MaxUpgradePoint} below " +
                    $"the legacy floor of {MinSkillLearnOrUpgradePoint} (also the divide-by-zero guard in " +
                    "SKILLSYSTEM::ReturnSkillValue).");

            if (skill.TotalHitNumber > MaxSkillTotalHitNumber)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has TotalHitNumber={skill.TotalHitNumber} outside " +
                    $"the legacy 0-{MaxSkillTotalHitNumber} bound.");

            if (skill.ValidRadius is < 0 or > MaxSkillValidRadius)
                throw new InvalidOperationException(
                    $"world.Skills row SkillId={skill.SkillId} has ValidRadius={skill.ValidRadius} outside the " +
                    $"legacy 0-{MaxSkillValidRadius} bound.");
        }

        foreach (var description in descriptions.OrderBy(static row => row.SkillId)
                     .ThenBy(static row => row.LineIndex))
            if (description.Text.Length > MaxSkillDescriptionLength)
                throw new InvalidOperationException(
                    $"world.SkillDescriptions row SkillId={description.SkillId} LineIndex={description.LineIndex} " +
                    $"has Text longer than {MaxSkillDescriptionLength} characters.");

        foreach (var grade in grades.OrderBy(static row => row.SkillId).ThenBy(static row => row.GradeIndex))
        {
            if (grade.ManaUse is < 0 or > MaxSkillGradeManaUse)
                throw new InvalidOperationException(
                    $"world.SkillGrades row SkillId={grade.SkillId} GradeIndex={grade.GradeIndex} has " +
                    $"ManaUse={grade.ManaUse} outside the legacy 0-{MaxSkillGradeManaUse} bound.");

            if (grade.StunAttack > MaxSkillGradeStun || grade.StunDefense > MaxSkillGradeStun)
                throw new InvalidOperationException(
                    $"world.SkillGrades row SkillId={grade.SkillId} GradeIndex={grade.GradeIndex} has a StunAttack/" +
                    $"StunDefense outside the legacy 0-{MaxSkillGradeStun} bound.");

            if (grade.FastRunSpeed is < 0 or > MaxSkillGradeFastRunSpeed)
                throw new InvalidOperationException(
                    $"world.SkillGrades row SkillId={grade.SkillId} GradeIndex={grade.GradeIndex} has " +
                    $"FastRunSpeed={grade.FastRunSpeed} outside the legacy 0-{MaxSkillGradeFastRunSpeed} bound.");

            if (grade.AttackInfo1 is < 0 or > MaxSkillGradeAttackInfo1)
                throw new InvalidOperationException(
                    $"world.SkillGrades row SkillId={grade.SkillId} GradeIndex={grade.GradeIndex} has " +
                    $"AttackInfo1={grade.AttackInfo1} outside the legacy 0-{MaxSkillGradeAttackInfo1} bound.");

            if (grade.RunTime is < 0 or > MaxSkillGradeRunTime)
                throw new InvalidOperationException(
                    $"world.SkillGrades row SkillId={grade.SkillId} GradeIndex={grade.GradeIndex} has " +
                    $"RunTime={grade.RunTime} outside the legacy 0-{MaxSkillGradeRunTime} bound.");
        }
    }

    /// <summary>
    ///     Per-row NPC validation across world.Npcs and its child tables (menu options, shop items, skill offers,
    ///     speeches, gamble costs): aborts the whole load on the first invalid row, matching the legacy loader's
    ///     no-skip-and-continue failure contract. Reproduces the 500-slot capacity cap and every
    ///     <c>Npc_CheckValidElement</c> bound that is representable-and-violable given the relational DTOs -- see
    ///     this method's own remarks for what is deliberately left out and why.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/Header/S15_MyShare.cpp:1836-1963 (<c>MyShm::Npc_CheckValidElement</c> -- empty-row
    ///     sentinel, 1-500 index bound, name/speech termination, tribe/type/sort-number/size scalars, menu 1-2,
    ///     shop-info 0-99999, skill-info form-1 and form-2 0-300, gamble-cost 0-100,000,000) ;
    ///     Server/Header/S15_MyShare.cpp:619-668 (<c>Load_Npc</c> -- the 500-row capacity at :622, per-row
    ///     validation over the loaded count at :648, first-invalid logs the row index and aborts at :650-654) ;
    ///     Server/Header/Protocol/STRUCT.h:206-212 (name 28-byte, speech 5×5×51-byte, menu 100, shop 3-page×28-slot
    ///     buffer/count constants, unconditional ahead of the STRUCT.h:218 <c>GXCW_SV</c> guard).
    ///     A companion set of storage-level CHECK constraints
    ///     (Database/Migrations/033_npc_static_data_range_checks.sql) already enforces the scalar/menu/gamble bounds
    ///     directly on the tables; this method reproduces the same fail-fast-at-boot contract in C# for parity and
    ///     defense-in-depth, matching <see cref="ValidateMonsters" />.
    ///     <para>
    ///         Deliberately NOT reproduced here, each for a cited reason:
    ///         <list type="bullet">
    ///             <item>
    ///                 The dense-positional-identity rule (index equals slot + 1, Server/Header/S15_MyShare.cpp:
    ///                 1851-1854) -- NpcId is the relational PRIMARY KEY, same disposition as SkillId/MonsterId/
    ///                 QuestId and as Database/Migrations/033. The 1-500 <em>range</em> cap IS reproduced.
    ///             </item>
    ///             <item>
    ///                 The speech-count 1-5 bound (Server/Header/S15_MyShare.cpp:1866-1869) -- the legacy
    ///                 <c>nSpeechNum</c> scalar is parsed then discarded by the import tool and has no column in the
    ///                 Fenrir schema (per Database/Migrations/033's own note), so there is nothing to validate; the
    ///                 count is implicit in the number of world.NpcSpeeches rows. Left as a documented gap.
    ///             </item>
    ///         </list>
    ///     </para>
    ///     Name/speech translation note is identical to <see cref="ValidateSkills" />'s
    ///     (<see cref="MaxNpcNameLength" /> = 27 for the 28-byte buffer, <see cref="MaxNpcSpeechLength" /> = 50 for
    ///     the 51-byte one). Shop-item and skill-offer ids are additionally FK-constrained in the schema (stricter
    ///     than legacy's raw 0-99999 / 0-300 numeric bound); the numeric bound is still reproduced here because the
    ///     C# builder has no foreign key to lean on.
    /// </remarks>
    private static void ValidateNpcs(
        IReadOnlyList<NpcRowDto> npcs,
        IReadOnlyList<NpcMenuOptionRowDto> menuOptions,
        IReadOnlyList<NpcShopItemRowDto> shopItems,
        IReadOnlyList<NpcSkillOfferRowDto> skillOffers,
        IReadOnlyList<NpcSpeechRowDto> speeches,
        IReadOnlyList<NpcGambleCostRowDto> gambleCosts)
    {
        foreach (var npc in npcs.OrderBy(static npc => npc.NpcId))
        {
            // NpcId == 0 marks an empty/unused slot in the legacy fixed 500-slot table -- accepted (skipped), same
            // parity shape as the SkillId == 0 / ItemId == 0 skips above.
            if (npc.NpcId == 0)
                continue;

            if (npc.NpcId is < MinReferenceIndex or > MaxNpcIndex)
                throw new InvalidOperationException(
                    $"world.Npcs row NpcId={npc.NpcId} is outside the legacy {MinReferenceIndex}-{MaxNpcIndex} " +
                    "index cap (the fixed NPC-table capacity).");

            if (npc.Name.Length > MaxNpcNameLength)
                throw new InvalidOperationException(
                    $"world.Npcs row NpcId={npc.NpcId} has a Name longer than {MaxNpcNameLength} characters.");

            if (npc.Tribe is < MinNpcTribe or > MaxNpcTribe)
                throw new InvalidOperationException(
                    $"world.Npcs row NpcId={npc.NpcId} has Tribe={npc.Tribe} outside the legacy {MinNpcTribe}-" +
                    $"{MaxNpcTribe} bound.");

            if (npc.Type is < MinNpcType or > MaxNpcType)
                throw new InvalidOperationException(
                    $"world.Npcs row NpcId={npc.NpcId} has Type={npc.Type} outside the legacy {MinNpcType}-" +
                    $"{MaxNpcType} bound.");

            if (npc.DataSortNumber2D is < MinNpcDataSortNumber or > MaxNpcDataSortNumber
                || npc.DataSortNumber3D is < MinNpcDataSortNumber or > MaxNpcDataSortNumber)
                throw new InvalidOperationException(
                    $"world.Npcs row NpcId={npc.NpcId} has a DataSortNumber2D/DataSortNumber3D outside the legacy " +
                    $"{MinNpcDataSortNumber}-{MaxNpcDataSortNumber} bound.");

            if (npc.Size1 is < MinNpcSize or > MaxNpcSize
                || npc.Size2 is < MinNpcSize or > MaxNpcSize
                || npc.Size3 is < MinNpcSize or > MaxNpcSize)
                throw new InvalidOperationException(
                    $"world.Npcs row NpcId={npc.NpcId} has a Size1/Size2/Size3 outside the legacy {MinNpcSize}-" +
                    $"{MaxNpcSize} bound.");
        }

        foreach (var speech in speeches.OrderBy(static row => row.NpcId).ThenBy(static row => row.SpeechGroup)
                     .ThenBy(static row => row.SpeechIndex))
            if (speech.Text.Length > MaxNpcSpeechLength)
                throw new InvalidOperationException(
                    $"world.NpcSpeeches row NpcId={speech.NpcId} SpeechGroup={speech.SpeechGroup} " +
                    $"SpeechIndex={speech.SpeechIndex} has Text longer than {MaxNpcSpeechLength} characters.");

        foreach (var option in menuOptions.OrderBy(static row => row.NpcId).ThenBy(static row => row.SlotIndex))
            if (option.OptionId is < MinNpcMenuOption or > MaxNpcMenuOption)
                throw new InvalidOperationException(
                    $"world.NpcMenuOptions row NpcId={option.NpcId} SlotIndex={option.SlotIndex} has " +
                    $"OptionId={option.OptionId} outside the legacy {MinNpcMenuOption}-{MaxNpcMenuOption} bound.");

        foreach (var shopItem in shopItems.OrderBy(static row => row.NpcId).ThenBy(static row => row.ShopPage)
                     .ThenBy(static row => row.SlotIndex))
            if (shopItem.ItemId is { } itemId && itemId is < 0 or > MaxNpcShopItemId)
                throw new InvalidOperationException(
                    $"world.NpcShopItems row NpcId={shopItem.NpcId} ShopPage={shopItem.ShopPage} " +
                    $"SlotIndex={shopItem.SlotIndex} has ItemId={itemId} outside the legacy 0-{MaxNpcShopItemId} " +
                    "bound.");

        foreach (var offer in skillOffers.OrderBy(static row => row.NpcId).ThenBy(static row => row.NpcSkillOfferId))
            if (offer.SkillId is { } skillId && skillId is < 0 or > MaxNpcSkillOfferId)
                throw new InvalidOperationException(
                    $"world.NpcSkillOffers row NpcId={offer.NpcId} NpcSkillOfferId={offer.NpcSkillOfferId} has " +
                    $"SkillId={skillId} outside the legacy 0-{MaxNpcSkillOfferId} bound.");

        foreach (var gamble in gambleCosts.OrderBy(static row => row.NpcId).ThenBy(static row => row.GambleTier)
                     .ThenBy(static row => row.CostIndex))
            if (gamble.Value is < 0 or > MaxNpcGambleCost)
                throw new InvalidOperationException(
                    $"world.NpcGambleCosts row NpcId={gamble.NpcId} GambleTier={gamble.GambleTier} " +
                    $"CostIndex={gamble.CostIndex} has Value={gamble.Value} outside the legacy 0-{MaxNpcGambleCost} " +
                    "bound.");
    }

    /// <summary>
    ///     Coarse lowest-index self-test: after all reference tables are validated, confirms the canonical
    ///     lowest-index element of the SKILL and NPC tables resolves, aborting boot if it does not. Mirrors legacy
    ///     <c>MyGame::Init</c>'s "the first element of each table actually loaded" smoke test, distinct from the
    ///     exhaustive per-row validation above.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:1611-1636 -- probes skill index 1 (:1611-1618), NPC index 1
    ///     (:1620-1627), and quest (type 0, step 1) (:1629-1636); each null lookup logs an error and returns boot
    ///     failure.
    ///     <para>
    ///         Adapted to Fenrir's relational model, which diverges from legacy in two deliberate, pre-existing
    ///         ways this self-test must respect rather than override:
    ///         <list type="bullet">
    ///             <item>
    ///                 world.Npcs and world.Quests are allowed to be empty here (neither is a critical dataset in
    ///                 <see cref="Build" />, and the test fixtures rely on that), unlike legacy where those tables
    ///                 are always populated. So the SKILL probe is unconditional (world.Skills IS a critical,
    ///                 must-be-non-empty dataset), while the NPC probe fires only when NPC rows are actually present
    ///                 -- an NPC table that loaded rows must still contain its canonical first element.
    ///             </item>
    ///             <item>
    ///                 The quest probe is deliberately NOT reproduced. Its legacy key is (type 0, step 1), but
    ///                 Fenrir's world.Quests.Type is CHECK-constrained to 1-2 (CK_Quests_Type,
    ///                 Database/Tables/world/Quests.sql) and can never be 0 -- legacy's quest-lookup "type" is a
    ///                 different indexing concept from Fenrir's relational Type column, and this contract does not
    ///                 establish the mapping. Reproducing it with a guessed mapping would risk aborting boot on
    ///                 valid data, so it is left for a follow-up <c>legacy-behavior-translator</c> contract.
    ///             </item>
    ///         </list>
    ///     </para>
    /// </remarks>
    private static void RunLowestIndexSelfTest(WorldDataRows rows)
    {
        if (!rows.Skills.Any(static skill => skill.SkillId == MinReferenceIndex))
            throw new InvalidOperationException(
                $"world.Skills lowest-index self-test failed: the canonical skill (SkillId {MinReferenceIndex}) " +
                "did not resolve -- the skill reference table loaded but is missing its first element, so the " +
                "GameServer must not begin serving (legacy MyGame::Init lowest-index probe).");

        if (rows.Npcs.Count > 0 && !rows.Npcs.Any(static npc => npc.NpcId == MinReferenceIndex))
            throw new InvalidOperationException(
                $"world.Npcs lowest-index self-test failed: the table is non-empty but the canonical NPC " +
                $"(NpcId {MinReferenceIndex}) did not resolve -- an NPC table that loaded rows must contain its " +
                "first element, so the GameServer must not begin serving (legacy MyGame::Init lowest-index probe).");
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
                // A1/Table B: world.MonsterSpawnRegions.ZoneNumber is populated from each on-disk *.WREGION.csv
                // file's own canonical "Z0NN_" filename prefix (Tools/.../MonsterSpawnRegionReader.cs) -- one
                // file per canonical mSameSummon group, not per physical zone. A physical-number lookup here
                // silently starves every non-canonical zone in a group of its spawn regions.
                TakeGroup(spawnRegionsByZone,
                    ZoneCanonicalSpawnRegionMap.ResolveCanonicalSpawnZoneId(zone.ZoneNumber))));

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
