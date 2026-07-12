using System.Text;
using Fenrir.Tools.DbMigrator.Legacy.Readers;

namespace Fenrir.Tools.DbMigrator.Legacy.Seeding;

public static class MonsterSeedGenerator
{
    private const string MonsterColumns =
        "MonsterId, Name, ChatLine1, ChatLine2, Type, SpecialType, DamageType, DataSortNumber, " +
        "Size1, Size2, Size3, Size4, SizeCategory, CheckCollision, TotalHitNum, TotalSkillHitNum, " +
        "ItemLevel, MartialItemLevel, RealLevel, MartialRealLevel, GeneralExperience, PatExperience, " +
        "Life, AttackType, RadiusInfo1, RadiusInfo2, WalkSpeed, RunSpeed, DeathSpeed, AttackPower, " +
        "DefensePower, AttackSuccess, AttackBlock, ElementAttackPower, ElementDefensePower, Critical, " +
        "FollowInfo1, FollowInfo2, SummonTime1, SummonTime2";

    private const string FrameColumns =
        "MonsterId, FrameInfo1, FrameInfo2, FrameInfo3, FrameInfo4, FrameInfo5, FrameInfo6, " +
        "HitFrame1, HitFrame2, HitFrame3, SkillHitFrame1, SkillHitFrame2, SkillHitFrame3, BulletInfo1, BulletInfo2";

    private const string MoneyColumns = "MonsterId, DropRate, MinAmount, MaxAmount";
    private const string PotionColumns = "MonsterId, SlotIndex, DropRate, PotionItemId";
    private const string ExtraColumns = "MonsterId, SlotIndex, DropRate, ItemId";
    private const string CategoryColumns = "MonsterId, CategoryIndex, Value";
    private const string QuestColumns = "MonsterId, DropRate, QuestItemId";

    public static void Generate(string dataDir, string outputPath)
    {
        var monsters = MonsterReader.ReadAll(dataDir).Where(m => m.Index != 0).OrderBy(m => m.Index).ToList();

        var realItemIds = ItemReader.ReadAll(dataDir).Where(i => i.Index != 0).Select(i => i.Index).ToHashSet();

        var monsterRows = monsters.Select(m => string.Join(", ", new[]
        {
            SqlSeedWriter.Number(m.Index), SqlSeedWriter.NString(m.Name), SqlSeedWriter.NString(m.ChatInfo[0]),
            SqlSeedWriter.NString(m.ChatInfo[1]), SqlSeedWriter.Number(m.Type), SqlSeedWriter.Number(m.SpecialType),
            SqlSeedWriter.Number(m.DamageType), SqlSeedWriter.Number(m.DataSortNumber), SqlSeedWriter.Number(m.Size[0]),
            SqlSeedWriter.Number(m.Size[1]), SqlSeedWriter.Number(m.Size[2]), SqlSeedWriter.Number(m.Size[3]),
            SqlSeedWriter.Number(m.SizeCategory), SqlSeedWriter.Number(m.CheckCollision),
            SqlSeedWriter.Number(m.TotalHitNum),
            SqlSeedWriter.Number(m.TotalSkillHitNum), SqlSeedWriter.Number(m.ItemLevel),
            SqlSeedWriter.Number(m.MartialItemLevel),
            SqlSeedWriter.Number(m.RealLevel), SqlSeedWriter.Number(m.MartialRealLevel),
            SqlSeedWriter.Number(m.GeneralExperience),
            SqlSeedWriter.Number(m.PatExperience), SqlSeedWriter.Number(m.Life), SqlSeedWriter.Number(m.AttackType),
            SqlSeedWriter.Number(m.RadiusInfo[0]), SqlSeedWriter.Number(m.RadiusInfo[1]),
            SqlSeedWriter.Number(m.WalkSpeed),
            SqlSeedWriter.Number(m.RunSpeed), SqlSeedWriter.Number(m.DeathSpeed), SqlSeedWriter.Number(m.AttackPower),
            SqlSeedWriter.Number(m.DefensePower), SqlSeedWriter.Number(m.AttackSuccess),
            SqlSeedWriter.Number(m.AttackBlock),
            SqlSeedWriter.Number(m.ElementAttackPower), SqlSeedWriter.Number(m.ElementDefensePower),
            SqlSeedWriter.Number(m.Critical),
            SqlSeedWriter.Number(m.FollowInfo[0]), SqlSeedWriter.Number(m.FollowInfo[1]),
            SqlSeedWriter.Number(m.SummonTime[0]),
            SqlSeedWriter.Number(m.SummonTime[1])
        })).ToList();

        var frameRows = monsters.Select(m => string.Join(", ", new[]
        {
            SqlSeedWriter.Number(m.Index), SqlSeedWriter.Number(m.FrameInfo[0]), SqlSeedWriter.Number(m.FrameInfo[1]),
            SqlSeedWriter.Number(m.FrameInfo[2]), SqlSeedWriter.Number(m.FrameInfo[3]),
            SqlSeedWriter.Number(m.FrameInfo[4]),
            SqlSeedWriter.Number(m.FrameInfo[5]), SqlSeedWriter.Number(m.HitFrame[0]),
            SqlSeedWriter.Number(m.HitFrame[1]),
            SqlSeedWriter.Number(m.HitFrame[2]), SqlSeedWriter.Number(m.SkillHitFrame[0]),
            SqlSeedWriter.Number(m.SkillHitFrame[1]),
            SqlSeedWriter.Number(m.SkillHitFrame[2]), SqlSeedWriter.Number(m.BulletInfo[0]),
            SqlSeedWriter.Number(m.BulletInfo[1])
        })).ToList();

        var moneyRows = monsters.Select(m => string.Join(", ",
            SqlSeedWriter.Number(m.Index), SqlSeedWriter.Number(m.DropMoneyInfo[0]),
            SqlSeedWriter.Number(m.DropMoneyInfo[1]), SqlSeedWriter.Number(m.DropMoneyInfo[2]))).ToList();

        var potionRows = new List<string>();
        foreach (var m in monsters)
            for (var slot = 0; slot < m.DropPotionInfo.Length; slot++)
            {
                var pair = m.DropPotionInfo[slot];
                if (pair[0] == 0 && pair[1] == 0) continue;
                potionRows.Add(string.Join(", ", SqlSeedWriter.Number(m.Index), slot.ToString(),
                    SqlSeedWriter.Number(pair[0]), SqlSeedWriter.Number(ResolveItemId(pair[1], realItemIds))));
            }

        var extraRows = new List<string>();
        foreach (var m in monsters)
            for (var slot = 0; slot < m.DropExtraItemInfo.Length; slot++)
            {
                var pair = m.DropExtraItemInfo[slot];
                if (pair[0] == 0 && pair[1] == 0) continue;
                extraRows.Add(string.Join(", ", SqlSeedWriter.Number(m.Index), slot.ToString(),
                    SqlSeedWriter.Number(pair[0]), SqlSeedWriter.Number(ResolveItemId(pair[1], realItemIds))));
            }

        var categoryRows = new List<string>();
        foreach (var m in monsters)
            for (var slot = 0; slot < m.DropItemInfo.Length; slot++)
            {
                var value = m.DropItemInfo[slot];
                if (value == 0) continue;
                categoryRows.Add(string.Join(", ", SqlSeedWriter.Number(m.Index), slot.ToString(),
                    SqlSeedWriter.Number(value)));
            }

        var questRows = monsters
            .Where(m => m.DropQuestItemInfo[0] != 0 || m.DropQuestItemInfo[1] != 0)
            .Select(m => string.Join(", ", SqlSeedWriter.Number(m.Index),
                SqlSeedWriter.Number(m.DropQuestItemInfo[0]), SqlSeedWriter.Number(m.DropQuestItemInfo[1])))
            .ToList();

        var sb = new StringBuilder();
        sb.Append("-- Generated by Tools/Fenrir.Tools.DbMigrator/Legacy/Seeding/MonsterSeedGenerator.cs\n");
        sb.Append("-- from Server/BuildEU33/DATA (005_00004.IMG). Regenerate rather than hand-edit.\n");
        sb.Append("-- One row per MONSTER_INFO record with Index != 0 (1139 of 10000 legacy array slots);\n");
        sb.Append("-- animation-frame columns and all 5 drop tables are normalized out -- see each\n");
        sb.Append("-- world.Monster*.sql table's own header for the per-table rationale. All 7 tables are\n");
        sb.Append("-- seeded together, guarded by a single check against world.Monsters, since they are\n");
        sb.Append("-- always populated from the same run.\n");
        sb.Append("IF NOT EXISTS (SELECT 1 FROM world.Monsters)\n");
        sb.Append("BEGIN\n");

        SqlSeedWriter.WriteInserts(sb, "world.Monsters", MonsterColumns, monsterRows);
        SqlSeedWriter.WriteInserts(sb, "world.MonsterAnimationFrames", FrameColumns, frameRows);
        SqlSeedWriter.WriteInserts(sb, "world.MonsterDropMoney", MoneyColumns, moneyRows);
        SqlSeedWriter.WriteInserts(sb, "world.MonsterDropPotions", PotionColumns, potionRows);
        SqlSeedWriter.WriteInserts(sb, "world.MonsterDropExtraItems", ExtraColumns, extraRows);
        SqlSeedWriter.WriteInserts(sb, "world.MonsterDropCategoryRates", CategoryColumns, categoryRows);
        SqlSeedWriter.WriteInserts(sb, "world.MonsterDropQuestItems", QuestColumns, questRows);

        sb.Append("END;\n");

        File.WriteAllText(outputPath, sb.ToString());
        Console.WriteLine($"Monsters.sql: {monsterRows.Count} monsters, {frameRows.Count} animation-frame rows, " +
                          $"{moneyRows.Count} money rows, {potionRows.Count} potion slots, {extraRows.Count} extra-item slots, " +
                          $"{categoryRows.Count} category-rate slots, {questRows.Count} quest-item rows -> {outputPath}");
    }

    private static int? ResolveItemId(int rawItemId, HashSet<int> realItemIds)
    {
        return rawItemId != 0 && realItemIds.Contains(rawItemId) ? rawItemId : null;
    }
}
