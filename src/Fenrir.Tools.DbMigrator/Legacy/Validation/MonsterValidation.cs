using System.Text;
using Fenrir.Tools.DbMigrator.Legacy.Readers;
using Fenrir.Tools.DbMigrator.Legacy.Records;

namespace Fenrir.Tools.DbMigrator.Legacy.Validation;

internal static class MonsterValidation
{
    private const int FullCsvFieldCount = 181;

    private const int
        DropSectionStartIndex = 54;

    public static void Run(string dataDir)
    {
        var monsters = MonsterReader.ReadAllRaw(dataDir);
        Console.WriteLine($"Parsed {monsters.Count} monster records from 005_00004.IMG.");

        var flattenedFieldCount = FlattenToCsvFields(monsters[0]).Count;
        if (flattenedFieldCount != FullCsvFieldCount)
            Console.WriteLine(
                $"  [WARN] flattened field count {flattenedFieldCount} != expected {FullCsvFieldCount} -- " +
                "field-order comparisons below may be misaligned.");

        var first = monsters[0];
        Console.WriteLine($"Sample monster[0]: Index={first.Index}, Name='{first.Name}', Life={first.Life}, " +
                          $"AttackPower={first.AttackPower}, DropMoneyInfo=[{string.Join(",", first.DropMoneyInfo)}], " +
                          $"DropExtraItemInfo[0]=[{string.Join(",", first.DropExtraItemInfo[0])}]");

        VerifyAgainstDropCsv(monsters, Path.Combine(dataDir, "..", "005_00004DROP.CSV"));
        VerifyAgainstFullCsv(monsters, Path.Combine(dataDir, "..", "005_00004.CSV"));
    }

    private static void VerifyAgainstDropCsv(IReadOnlyList<MonsterRecord> monsters, string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"[SKIP] drop CSV not found at {csvPath}");
            return;
        }

        var byIndex = monsters.GroupBy(m => m.Index).ToDictionary(g => g.Key, g => g.First());
        var mismatches = 0;
        var numericMismatches = 0;
        var textMismatches = 0;
        var checkedRows = 0;
        var skippedRows = 0;
        var missingRows = 0;

        foreach (var line in File.ReadLines(csvPath, Encoding.Latin1))
        {
            var fields = line.Split('|');
            if (fields.Length == 0 || !int.TryParse(fields[0], out var index))
            {
                skippedRows++;
                continue;
            }

            if (!byIndex.TryGetValue(index, out var monster))
            {
                Console.WriteLine($"  [MISS] index {index} present in DROP CSV but not parsed");
                missingRows++;
                continue;
            }

            checkedRows++;
            var expectedName = monster.Name;
            var actualName = fields.Length > 1 ? fields[1].Trim('"') : string.Empty;
            if (expectedName != actualName)
            {
                textMismatches++;
                mismatches++;
                Console.WriteLine($"  [DIFF-text] index {index} Name: parsed='{expectedName}' csv='{actualName}'");
            }

            var expectedDropFields = FlattenToCsvFields(monster).Skip(DropSectionStartIndex).ToList();
            for (var i = 0; i < expectedDropFields.Count && i + 2 < fields.Length; i++)
            {
                var actual = fields[i + 2].Trim('"');
                if (expectedDropFields[i] == actual) continue;
                numericMismatches++;
                mismatches++;
                Console.WriteLine(
                    $"  [DIFF-NUMERIC] index {index} drop-field #{i}: parsed='{expectedDropFields[i]}' csv='{actual}'");
            }
        }

        Console.WriteLine(
            $"Cross-validated {checkedRows} rows ({skippedRows} unparsable, {missingRows} missing) against {csvPath}: " +
            $"{mismatches} total mismatches ({textMismatches} text, {numericMismatches} numeric).");
    }

    private static void VerifyAgainstFullCsv(IReadOnlyList<MonsterRecord> monsters, string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"[SKIP] full CSV not found at {csvPath}");
            return;
        }

        var byIndex = monsters.GroupBy(m => m.Index).ToDictionary(g => g.Key, g => g.First());
        var mismatches = 0;
        var numericMismatches = 0;
        var textMismatches = 0;
        var checkedRows = 0;
        var skippedRows = 0;
        var missingRows = 0;

        var lines = File.ReadLines(csvPath, Encoding.Latin1).Skip(1);

        foreach (var line in lines)
        {
            var fields = line.Split('|');
            if (fields.Length == 0 || !int.TryParse(fields[0], out var index))
            {
                skippedRows++;
                continue;
            }

            if (!byIndex.TryGetValue(index, out var monster))
            {
                Console.WriteLine($"  [MISS] index {index} present in full CSV but not parsed");
                missingRows++;
                continue;
            }

            var expected = FlattenToCsvFields(monster);
            checkedRows++;
            for (var i = 0; i < Math.Min(expected.Count, fields.Length); i++)
            {
                var actual = fields[i].Trim('"');
                if (expected[i] == actual) continue;
                var isTextField = i is 1 or 2 or 3;
                if (isTextField) textMismatches++;
                else numericMismatches++;
                mismatches++;
                Console.WriteLine(
                    $"  [DIFF{(isTextField ? "-text" : "-NUMERIC")}] index {index} field #{i}: parsed='{expected[i]}' csv='{actual}'");
            }
        }

        Console.WriteLine(
            $"Cross-validated {checkedRows} rows ({skippedRows} unparsable, {missingRows} missing) against {csvPath}: " +
            $"{mismatches} total mismatches ({textMismatches} text, {numericMismatches} numeric).");
    }

    private static List<string> FlattenToCsvFields(MonsterRecord monster)
    {
        List<string> fields =
        [
            monster.Index.ToString(), monster.Name, monster.ChatInfo[0], monster.ChatInfo[1],
            monster.Type.ToString(), monster.SpecialType.ToString(), monster.DamageType.ToString(),
            monster.DataSortNumber.ToString()
        ];
        fields.AddRange(monster.Size.Select(v => v.ToString()));
        fields.Add(monster.SizeCategory.ToString());
        fields.Add(monster.CheckCollision.ToString());
        fields.AddRange(monster.FrameInfo.Select(v => v.ToString()));
        fields.Add(monster.TotalHitNum.ToString());
        fields.AddRange(monster.HitFrame.Select(v => v.ToString()));
        fields.Add(monster.TotalSkillHitNum.ToString());
        fields.AddRange(monster.SkillHitFrame.Select(v => v.ToString()));
        fields.AddRange(monster.BulletInfo.Select(v => v.ToString()));
        fields.AddRange(monster.SummonTime.Select(v => v.ToString()));
        fields.AddRange([
            monster.ItemLevel.ToString(), monster.MartialItemLevel.ToString(), monster.RealLevel.ToString(),
            monster.MartialRealLevel.ToString(), monster.GeneralExperience.ToString(), monster.PatExperience.ToString(),
            monster.Life.ToString(), monster.AttackType.ToString()
        ]);
        fields.AddRange(monster.RadiusInfo.Select(v => v.ToString()));
        fields.AddRange([
            monster.WalkSpeed.ToString(), monster.RunSpeed.ToString(), monster.DeathSpeed.ToString(),
            monster.AttackPower.ToString(), monster.DefensePower.ToString(), monster.AttackSuccess.ToString(),
            monster.AttackBlock.ToString(), monster.ElementAttackPower.ToString(),
            monster.ElementDefensePower.ToString(),
            monster.Critical.ToString()
        ]);
        fields.AddRange(monster.FollowInfo.Select(v => v.ToString()));
        fields.AddRange(monster.DropMoneyInfo.Select(v => v.ToString()));
        foreach (var pair in monster.DropPotionInfo) fields.AddRange(pair.Select(v => v.ToString()));
        fields.AddRange(monster.DropItemInfo.Select(v => v.ToString()));
        fields.AddRange(monster.DropQuestItemInfo.Select(v => v.ToString()));
        foreach (var pair in monster.DropExtraItemInfo) fields.AddRange(pair.Select(v => v.ToString()));

        return fields;
    }
}
