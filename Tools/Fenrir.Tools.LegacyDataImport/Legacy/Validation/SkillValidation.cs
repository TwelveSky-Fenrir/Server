using System.Text;
using Fenrir.Tools.LegacyDataImport.Legacy.Readers;
using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Validation;

/// <summary>
///     Ad-hoc cross-validation of <see cref="SkillReader" /> against the <c>005_00003TH.csv</c> ts25ztool dump.
///     Mirrors the item validator's approach in Program.cs (Latin1 line reading, naive '|' split, quote-trimming).
///     Note: although <c>SKILL_INFO.sDescription</c> is a <c>char[10][51]</c> in memory, the CSV export only ever
///     dumps the first 3 description lines (columns 2-4) -- the remaining 7 slots are parsed faithfully from the
///     binary but are not present in the CSV, so only <see cref="SkillRecord.Description" />[0..2] are compared.
/// </summary>
internal static class SkillValidation
{
    public static void Run(string dataDir)
    {
        var skills = SkillReader.ReadAllRaw(dataDir);
        Console.WriteLine($"Parsed {skills.Count} skill slots from 005_00003.IMG.");

        var csvPath = Path.Combine(dataDir, "..", "005_00003TH.csv");
        if (!File.Exists(csvPath))
            // Fall back to a path passed in as the data directory's sibling, in case dataDir already points
            // at the BuildEU33 root rather than BuildEU33/DATA.
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
        // Latin1: the legacy CSV export (like our own IMG parse) is a raw byte dump, not UTF-8 -- reading it as
        // UTF-8 would corrupt every non-ASCII byte sequence into U+FFFD before comparison even starts.
        var mismatches = 0;
        var numericMismatches = 0;
        var checkedRows = 0;
        var skippedRows = 0;
        var rowNumber = 0;
        var lines = File.ReadLines(csvPath, Encoding.Latin1);

        foreach (var line in lines)
        {
            var csvRowIndex = rowNumber; // CSV has no header and no explicit key column tying 1:1 to slot order
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
                    i >= 5; // fields 0..4 are Index/Name/Description[0..2]; everything after is numeric
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
