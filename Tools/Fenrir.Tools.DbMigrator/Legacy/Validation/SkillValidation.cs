using System.Text;
using Fenrir.Tools.DbMigrator.Legacy.Readers;
using Fenrir.Tools.DbMigrator.Legacy.Records;

namespace Fenrir.Tools.DbMigrator.Legacy.Validation;

internal static class SkillValidation
{
    public static void Run(string dataDir)
    {
        var skills = SkillReader.ReadAllRaw(dataDir);
        Console.WriteLine($"Parsed {skills.Count} skill slots from 005_00003.IMG.");

        var csvPath = Path.Combine(dataDir, "..", "005_00003TH.csv");
        if (!File.Exists(csvPath))
            csvPath = Path.Combine(dataDir, "005_00003TH.csv");

        if (!File.Exists(csvPath))
        {
            Console.WriteLine(
                $"  [SKIP] could not locate 005_00003TH.csv near '{dataDir}' -- skipping cross-validation.");
            return;
        }

        VerifyAgainstCsv(skills, csvPath);
    }

    private static void VerifyAgainstCsv(IReadOnlyList<SkillRecord> skills, string csvPath)
    {
        var mismatches = 0;
        var numericMismatches = 0;
        var checkedRows = 0;
        var skippedRows = 0;
        var rowNumber = 0;
        var lines = File.ReadLines(csvPath, Encoding.Latin1);

        foreach (var line in lines)
        {
            var csvRowIndex = rowNumber;
            rowNumber++;

            var fields = line.Split('|');
            if (fields.Length == 0)
            {
                skippedRows++;
                continue;
            }

            if (csvRowIndex >= skills.Count)
            {
                Console.WriteLine(
                    $"  [MISS] CSV row {csvRowIndex} has no corresponding parsed slot (only {skills.Count} parsed)");
                mismatches++;
                continue;
            }

            var skill = skills[csvRowIndex];
            var expected = FlattenToCsvFields(skill);
            checkedRows++;
            for (var i = 0; i < Math.Min(expected.Count, fields.Length); i++)
            {
                var actual = fields[i].Trim('"');
                if (expected[i] == actual) continue;
                var isNumericField =
                    i >= 5;
                if (isNumericField) numericMismatches++;
                Console.WriteLine(
                    $"  [DIFF{(isNumericField ? "-NUMERIC" : "-text")}] row {csvRowIndex} field #{i}: parsed='{expected[i]}' csv='{actual}'");
                mismatches++;
            }
        }

        Console.WriteLine(
            $"Cross-validated {checkedRows} rows ({skippedRows} unparsable rows skipped) against {csvPath}: " +
            $"{mismatches} total mismatches, {numericMismatches} on NUMERIC (stat) fields.");
    }

    private static List<string> FlattenToCsvFields(SkillRecord skill)
    {
        List<string> fields =
        [
            skill.Index.ToString(), skill.Name, skill.Description[0], skill.Description[1], skill.Description[2],
            skill.Type.ToString(), skill.AttackType.ToString(), skill.DataNumber2D.ToString()
        ];
        fields.AddRange(skill.TribeInfo.Select(v => v.ToString()));
        fields.AddRange([
            skill.LearnSkillPoint.ToString(), skill.MaxUpgradePoint.ToString(), skill.TotalHitNumber.ToString(),
            skill.ValidRadius.ToString()
        ]);
        foreach (var grade in skill.GradeInfo) fields.AddRange(FlattenGrade(grade));
        return fields;
    }

    private static IEnumerable<string> FlattenGrade(SkillGradeRecord grade)
    {
        List<string> fields = [grade.ManaUse.ToString()];
        fields.AddRange(grade.RecoverInfo.Select(v => v.ToString()));
        fields.AddRange([grade.StunAttack.ToString(), grade.StunDefense.ToString(), grade.FastRunSpeed.ToString()]);
        fields.AddRange(grade.AttackInfo.Select(v => v.ToString()));
        fields.AddRange([
            grade.RunTime.ToString(), grade.ChargingDamageUp.ToString(), grade.AttackPowerUp.ToString(),
            grade.DefensePowerUp.ToString(), grade.AttackSuccessUp.ToString(), grade.AttackBlockUp.ToString(),
            grade.ElementAttackUp.ToString(), grade.ElementDefenseUp.ToString(), grade.AttackSpeedUp.ToString(),
            grade.RunSpeedUp.ToString(), grade.ShieldLifeUp.ToString(), grade.LuckUp.ToString(),
            grade.CriticalUp.ToString(), grade.ReturnSuccessUp.ToString(), grade.StunDefenseUp.ToString(),
            grade.DestroySuccessUp.ToString()
        ]);
        return fields;
    }
}
