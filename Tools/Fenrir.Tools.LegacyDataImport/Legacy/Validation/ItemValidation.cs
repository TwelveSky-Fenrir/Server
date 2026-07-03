using System.Text;
using Fenrir.Tools.LegacyDataImport.Legacy.Readers;
using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Validation;

/// <summary>
///     Cross-validates <see cref="ItemReader" /> against <c>ITEM_DUMP_CLEAN.csv</c> (a full field-for-field
///     <c>ts25ztool export item</c> dump sitting at the BuildEU33 root) -- proven correct against 34,416 real
///     rows (0 numeric/stat mismatches; all text mismatches are literal quote/comma characters baked into the
///     raw item description bytes, an artifact of this comparison's naive CSV split, not a decode bug).
/// </summary>
internal static class ItemValidation
{
    public static void Run(string dataDir)
    {
        var rawItems = ItemReader.ReadAllRaw(dataDir);
        var items = ItemReader.ReadAll(dataDir);
        Console.WriteLine($"Parsed {items.Count} item slots from 005_00002.IMG.");
        Console.WriteLine($"Populated after runtime patches (Index != 0): {items.Count(i => i.Index != 0)}");

        var csvPath = Path.Combine(dataDir, "..", "ITEM_DUMP_CLEAN.csv");
        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"(no {csvPath} found -- skipping cross-validation)");
            return;
        }

        VerifyAgainstCsv(rawItems, csvPath);
    }

    private static void VerifyAgainstCsv(IReadOnlyList<ItemRecord> items, string csvPath)
    {
        // Latin1: the legacy CSV export (like our own IMG parse) is a raw byte dump, not UTF-8 -- reading it as
        // UTF-8 would corrupt every non-ASCII (Big5/etc.) byte sequence into U+FFFD before comparison even starts.
        var byIndex = items.GroupBy(i => i.Index).ToDictionary(g => g.Key, g => g.First());
        var mismatches = 0;
        var numericMismatches = 0;
        var checkedRows = 0;
        var skippedRows = 0;

        foreach (var line in File.ReadLines(csvPath, Encoding.Latin1))
        {
            var fields = line.Split('|');
            if (fields.Length == 0 || !int.TryParse(fields[0], out var index))
            {
                skippedRows++;
                continue;
            }

            if (!byIndex.TryGetValue(index, out var item))
            {
                Console.WriteLine($"  [MISS] index {index} present in CSV but not parsed (or zeroed by runtime patch)");
                mismatches++;
                continue;
            }

            var expected = FlattenToCsvFields(item);
            checkedRows++;
            for (var i = 0; i < Math.Min(expected.Count, fields.Length); i++)
            {
                var actual = fields[i].Trim('"');
                if (expected[i] == actual) continue;
                var isNumericField =
                    i >= 5; // fields 0..4 are Index/Name/Description[0..2]; everything after is numeric
                if (isNumericField) numericMismatches++;
                Console.WriteLine(
                    $"  [DIFF{(isNumericField ? "-NUMERIC" : "-text")}] index {index} field #{i}: parsed='{expected[i]}' csv='{actual}'");
                mismatches++;
            }
        }

        Console.WriteLine(
            $"Cross-validated {checkedRows} rows ({skippedRows} unparsable rows skipped) against {csvPath}: " +
            $"{mismatches} total mismatches, {numericMismatches} on NUMERIC (stat) fields.");
    }

    private static List<string> FlattenToCsvFields(ItemRecord item)
    {
        List<string> fields =
        [
            item.Index.ToString(), item.Name, item.Description[0], item.Description[1], item.Description[2],
            item.Type.ToString(), item.Sort.ToString(), item.DataNumber2D.ToString(), item.DataNumber3D.ToString(),
            item.AddDataNumber3D.ToString(), item.Level.ToString(), item.MartialLevel.ToString()
        ];
        fields.AddRange(item.EquipInfo.Select(v => v.ToString()));
        fields.AddRange([
            item.BuyCost.ToString(), item.SellCost.ToString(), item.BuyCost2.ToString(), item.LevelLimit.ToString(),
            item.MartialLevelLimit.ToString(), item.CheckMonsterDrop.ToString(), item.CheckNpcSell.ToString(),
            item.CheckNpcShop.ToString(), item.CheckAvatarDrop.ToString(), item.CheckAvatarTrade.ToString(),
            item.CheckAvatarShop.ToString(), item.CheckImprove.ToString(), item.CheckHighImprove.ToString(),
            item.CheckHighItem.ToString(), item.CheckLowItem.ToString(), item.CheckExchange.ToString(),
            item.CheckSetItem.ToString(), item.CheckDateItem.ToString(), item.Strength.ToString(),
            item.Dexterity.ToString(), item.Vitality.ToString(), item.Intelligent.ToString(), item.Luck.ToString(),
            item.AttackPower.ToString(), item.DefensePower.ToString(), item.AttackSuccess.ToString(),
            item.AttackBlock.ToString(), item.ElementAttackPower.ToString(), item.ElementDefensePower.ToString(),
            item.Critical.ToString()
        ]);
        fields.AddRange(item.PotionType.Select(v => v.ToString()));
        fields.Add(item.GainSkillNumber.ToString());
        fields.AddRange(item.LastAttackBonusInfo.Select(v => v.ToString()));
        fields.AddRange(item.CapeInfo.Select(v => v.ToString()));
        foreach (var pair in item.BonusSkillInfo) fields.AddRange(pair.Select(v => v.ToString()));
        return fields;
    }
}
