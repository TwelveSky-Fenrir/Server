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
    ///     was read, matching the legacy loader's no-skip-and-continue failure contract. LEVEL was the first of
    ///     the LEVEL/ITEM/SKILL/MONSTER/NPC/QUEST/GSOCKET "a single malformed row aborts the whole dataset"
    ///     systems whose per-row rule set (<c>Level_CheckValidElement</c>) was read in full for the behavior
    ///     contract backing this method; QUEST (<see cref="ValidateQuests" />), ITEM (<see cref="ValidateItems" />),
    ///     MONSTER (<see cref="ValidateMonsters" />), and GSOCKET (<see cref="ValidateGemSockets" />) since gained
    ///     their own partial per-row validation, each covering only the specific bounds their backing contract
    ///     confirmed -- see those methods' own remarks for what remains unreproduced in each. SKILL/NPC remain
    ///     wholly open gaps.
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
    ///     SKILL/NPC intentionally get no per-row validation here (ITEM, QUEST, MONSTER, and GSOCKET now have
    ///     their own partial validation, see <see cref="ValidateItems" />/<see cref="ValidateQuests" />/
    ///     <see cref="ValidateMonsters" />/<see cref="ValidateGemSockets" />): the behavior contract backing this
    ///     method explicitly flags those two remaining systems' <c>*_CheckValidElement</c> rule sets as
    ///     unread/unverified, so nothing was guessed at for them -- closing that remains an open gap for a
    ///     follow-up <c>legacy-behavior-translator</c> contract per system.
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
    ///     dataset (LEVEL/ITEM/MONSTER/GSOCKET today, see also <see cref="ValidateItems" />/
    ///     <see cref="ValidateMonsters" />/<see cref="ValidateGemSockets" />; SKILL/NPC remain open gaps per
    ///     <see cref="ValidateLevels" />'s own remarks).
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
    ///     per-row-validated dataset (LEVEL/QUEST/ITEM above; SKILL/NPC remain open gaps per
    ///     <see cref="ValidateLevels" />'s own remarks).
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
