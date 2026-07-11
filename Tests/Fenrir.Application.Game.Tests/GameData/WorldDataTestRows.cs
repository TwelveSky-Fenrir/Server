using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.GameData;

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
        return new MonsterRowDto(
            monsterId, $"Monster{monsterId}", null, null,
            1, 1, 1, 1,
            1, 1, 1, 0, 1, 1,
            0, 0, 1, 0,
            1, 0, 0, 0, 100,
            1, 0, 0, 0, 0, 0,
            0, 0, 0, 0,
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

        internal static WorldDataRows MinimalRows()
    {
        return new WorldDataRows
        {
            Items = [Item(1)],
            ItemBonusSkills = [],
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
