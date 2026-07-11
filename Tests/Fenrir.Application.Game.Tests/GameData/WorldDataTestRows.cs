using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.GameData;

/// <summary>In-memory row factories for the WorldDataCacheBuilder tests; non-essential columns are zeroed.</summary>
internal static class WorldDataTestRows
{
    internal static ItemRowDto Item(int itemId)
    {
        return new ItemRowDto(
            itemId, $"Item{itemId}", null, null, null,
            0, 0, 0, 0, 0,
            1, 0, 0, 0,
            0, 0, 0, 1, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0,
            0, 0, null,
            0, 0, 0, 0, 0);
    }

    internal static MonsterRowDto Monster(int monsterId)
    {
        // Most fields use the smallest legacy-valid value (1) rather than 0: ValidateMonsters (backed by
        // Monster_CheckValidElement, Server/Header/S15_MyShare.cpp:1519-1830) rejects 0 for Type/SpecialType/
        // DamageType/DataSortNumber/Size1-3/SizeCategory/CheckCollision/ItemLevel/AttackType/SummonTime1-2/
        // FrameInfo1-6/BulletInfo1-2. Fields whose legacy-valid range does include 0 (Size4, TotalHitNum/
        // TotalSkillHitNum, MartialItemLevel/MartialRealLevel, GeneralExperience/PatExperience, RadiusInfo1-2,
        // WalkSpeed/RunSpeed/DeathSpeed, AttackPower and the other combat stats, Critical, HitFrame1-3,
        // SkillHitFrame1-3) are left at 0.
        return new MonsterRowDto(
            monsterId, $"Monster{monsterId}", null, null,
            1, 1, 1, 1,
            1, 1, 1, 0, 1, 1,
            0, 0, 1, 0,
            1, 0, 0, 0, 100,
            1, 0, 0, 0, 0, 0,
            0, 0, 0, 0,
            // FollowInfo1/2 (anti-clump pursuer-cap bounds, S10_MySummon.cpp:795) default to a small non-zero
            // range rather than 0 -- MonsterEntity.Create rolls MonsterEntity.PursuerCapacity from these, and a
            // 0 cap would make MonsterAiSystem's anti-clump filter refuse every candidate for every test that
            // doesn't override these fields.
            0, 0, 0, 5, 5,
            1, 1,
            1, 1, 1, 1, 1, 1,
            0, 0, 0, 0, 0, 0,
            1, 1);
    }

    internal static SkillRowDto Skill(int skillId)
    {
        return new SkillRowDto(
            skillId, $"Skill{skillId}",
            0, 0, 0, 0, 0,
            0, 0, 0, 0);
    }

    /// <summary>
    ///     A skill row whose every field sits at the smallest legacy-valid value, so it passes
    ///     <c>WorldDataCacheBuilder.ValidateSkills</c> (backed by <c>Skill_CheckValidElement</c>,
    ///     Server/Header/S15_MyShare.cpp:1277-1497). Unlike <see cref="Skill" /> (which zeroes non-essential fields
    ///     for the many direct <c>SkillDefinition</c> consumers that never touch Build), this is the row shape used
    ///     on the <c>Build</c> validation path -- Type/AttackType/DataNumber2D/TribeInfo1/TribeInfo2/LearnSkillPoint/
    ///     MaxUpgradePoint are all rejected at 0. TotalHitNumber/ValidRadius stay 0 (their legacy ranges include 0).
    /// </summary>
    internal static SkillRowDto ValidSkill(int skillId)
    {
        return new SkillRowDto(
            skillId, $"Skill{skillId}",
            1, 1, 1, 1, 1,
            1, 1, 0, 0);
    }

    internal static SkillGradeRowDto SkillGrade(int skillId, byte gradeIndex)
    {
        return new SkillGradeRowDto(
            skillId, gradeIndex,
            0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0,
            0, 0, 0);
    }

    internal static NpcRowDto Npc(int npcId)
    {
        return new NpcRowDto(npcId, $"Npc{npcId}", 1, 1,
            0, 0, 0, 0, 0);
    }

    /// <summary>
    ///     An NPC row whose every field sits at the smallest legacy-valid value, so it passes
    ///     <c>WorldDataCacheBuilder.ValidateNpcs</c> (backed by <c>Npc_CheckValidElement</c>,
    ///     Server/Header/S15_MyShare.cpp:1836-1963). <see cref="Npc" /> zeroes DataSortNumber2D/3D and Size1-3 for
    ///     the direct <c>NpcDefinition</c> consumers that never touch Build; those fields are rejected at 0 on the
    ///     Build validation path, so this row sets them to 1.
    /// </summary>
    internal static NpcRowDto ValidNpc(int npcId)
    {
        return new NpcRowDto(npcId, $"Npc{npcId}", 1, 1,
            1, 1, 1, 1, 1);
    }

    internal static QuestRowDto Quest(int questId)
    {
        return new QuestRowDto(
            questId, $"Quest{questId}", 1, 1, 1, 1, 1,
            null, null, null, null,
            1, null, null, null,
            null, null, 1,
            null, null, null, null, null);
    }

    internal static LevelRowDto Level(short level)
    {
        return new LevelRowDto(level, 0, 100, 0,
            0, 0, 0, 0, 0,
            100, 100);
    }

    internal static ZoneRowDto Zone(short zoneNumber)
    {
        return new ZoneRowDto(zoneNumber, 0f, 0f, 0f);
    }

    internal static ZonePortalRowDto Portal(short zoneNumber, short slotIndex, short? targetZoneNumber)
    {
        return new ZonePortalRowDto(zoneNumber, slotIndex, 0f, 0f, 0f,
            targetZoneNumber);
    }

    internal static ZoneSpawnPointRowDto SpawnPoint(short zoneNumber, short slotIndex, short? fromZoneNumber)
    {
        return new ZoneSpawnPointRowDto(zoneNumber, slotIndex, fromZoneNumber, 0f, 0f, 0f);
    }

    internal static ZoneNpcSpawnRowDto NpcSpawn(short zoneNumber, short slotIndex, int? npcId)
    {
        return new ZoneNpcSpawnRowDto(zoneNumber, slotIndex, npcId, 0f, 0f, 0f, 0f);
    }

    internal static MonsterSpawnRegionRowDto SpawnRegion(int regionId, short? zoneNumber, int? monsterId)
    {
        return new MonsterSpawnRegionRowDto(regionId, zoneNumber, $"Z{zoneNumber ?? 0:000}_SUMMON.WREGION.csv",
            0, monsterId, 0, 1, 0, 0, 0, 10);
    }

    /// <summary>Smallest WorldDataRows that passes Build's critical-dataset gate; tests override slices via <c>with</c>.</summary>
    internal static WorldDataRows MinimalRows()
    {
        return new WorldDataRows
        {
            Items = [Item(1)],
            ItemBonusSkills = [],
            // A legacy-valid skill (not the zero-field Skill(1)): MinimalRows flows through Build, which now runs
            // ValidateSkills + the lowest-index self-test, both of which reject the zero-field row and require
            // SkillId 1 to be present.
            Skills = [ValidSkill(1)],
            SkillDescriptions = [],
            SkillGrades = [],
            Monsters = [Monster(1)],
            MonsterDropMoney = [],
            MonsterDropPotions = [],
            MonsterDropExtraItems = [],
            MonsterDropCategoryRates = [],
            MonsterDropQuestItems = [],
            Npcs = [],
            NpcMenuOptions = [],
            NpcShopItems = [],
            NpcSkillOffers = [],
            NpcSpeeches = [],
            NpcGambleCosts = [],
            Quests = [],
            QuestRewards = [],
            QuestSpeeches = [],
            Levels = [Level(1)],
            Zones = [Zone(1)],
            ZonePortals = [Portal(1, 0, 1)],
            ZoneSpawnPoints = [],
            ZoneNpcSpawns = [],
            MonsterSpawnRegions = [],
            GemSockets = [],
            BloodExchangeCatalog = [],
            EventDefinitions = [],
            ItemMallProducts = [],
            RewardBundles = [],
            RewardBundleItems = []
        };
    }
}
