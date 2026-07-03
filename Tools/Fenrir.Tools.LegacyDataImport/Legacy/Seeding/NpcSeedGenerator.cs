using System.Text;
using Fenrir.Tools.LegacyDataImport.Legacy.Readers;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Seeding;

/// <summary>
///     Generates <c>70_seed/world/Npcs.sql</c> from the real <c>005_00005.IMG</c> data
///     (<see cref="NpcReader.ReadAll" />). One row per NPC_INFO record whose legacy Index is nonzero --
///     369 of the 500 array slots are unused placeholders (Index==0 AND Name=="" for every single one,
///     confirmed 1:1 equivalent by inspecting the real decoded data, not assumed) -- leaving 131 real NPCs.
///     Every fixed-size sub-array (Speech, Menu, ShopInfo, SkillInfo1, SkillInfo2, GambleCostInfo) is
///     normalized into its own child table, one row per populated slot, per "normalize, don't
///     transliterate" -- except Size[3], a small always-meaningful triple kept as three plain columns
///     (see world.Npcs.sql). Two sub-arrays turned out surprising on inspection: Menu (nMenu[100]) and
///     GambleCostInfo (nGambleCostInfo[145][15]) are both 100% dense wherever they're used at all (0
///     filtering removes nothing), unlike ShopInfo/SkillInfo1/SkillInfo2 which are genuinely sparse even
///     within the NPCs that use them -- see world.NpcMenuOptions.sql/world.NpcGambleCosts.sql for the
///     detailed findings.
/// </summary>
public static class NpcSeedGenerator
{
    private const string NpcColumns =
        "NpcId, Name, Tribe, Type, DataSortNumber2D, DataSortNumber3D, Size1, Size2, Size3";

    private const string SpeechColumns = "NpcId, SpeechGroup, SpeechIndex, Text";
    private const string MenuColumns = "NpcId, SlotIndex, OptionId";
    private const string ShopColumns = "NpcId, ShopPage, SlotIndex, ItemId";
    private const string SkillOfferColumns = "NpcId, ArrayKind, Tier, Dim2, Dim3, SlotIndex, SkillId";
    private const string GambleColumns = "NpcId, GambleTier, CostIndex, Value";

    public static void Generate(string dataDir, string outputPath)
    {
        var real = NpcReader.ReadAll(dataDir).Where(n => n.Index != 0).OrderBy(n => n.Index).ToList();

        var npcRows = real.Select(n => string.Join(", ", new[]
        {
            SqlSeedWriter.Number(n.Index),
            SqlSeedWriter.NString(n.Name),
            SqlSeedWriter.Number(n.Tribe),
            SqlSeedWriter.Number(n.Type),
            SqlSeedWriter.Number(n.DataSortNumber2D),
            SqlSeedWriter.Number(n.DataSortNumber3D),
            SqlSeedWriter.Number(n.Size[0]),
            SqlSeedWriter.Number(n.Size[1]),
            SqlSeedWriter.Number(n.Size[2])
        })).ToList();

        var speechRows = new List<string>();
        var menuRows = new List<string>();
        var shopRows = new List<string>();
        var skillRows = new List<string>();
        var gambleRows = new List<string>();

        foreach (var n in real)
        {
            for (var g = 0; g < n.Speech.Length; g++)
            for (var i = 0; i < n.Speech[g].Length; i++)
            {
                var text = n.Speech[g][i];
                if (string.IsNullOrEmpty(text)) continue;
                speechRows.Add(string.Join(", ",
                    SqlSeedWriter.Number(n.Index), g.ToString(), i.ToString(), SqlSeedWriter.NString(text)));
            }

            for (var slot = 0; slot < n.Menu.Length; slot++)
            {
                var value = n.Menu[slot];
                if (value == 0) continue;
                menuRows.Add(string.Join(", ", SqlSeedWriter.Number(n.Index), slot.ToString(), value.ToString()));
            }

            for (var page = 0; page < n.ShopInfo.Length; page++)
            for (var slot = 0; slot < n.ShopInfo[page].Length; slot++)
            {
                var itemId = n.ShopInfo[page][slot];
                if (itemId == 0) continue;
                shopRows.Add(string.Join(", ",
                    SqlSeedWriter.Number(n.Index), page.ToString(), slot.ToString(), itemId.ToString()));
            }

            // ArrayKind 1 = nSkillInfo1[tier][slot] (Dim2/Dim3 NULL). ArrayKind 2 = nSkillInfo2[a][b][c][slot],
            // flattened by NpcReader as a*72+b*24+c*8+slot -- see world.NpcSkillOffers.sql for the full
            // rationale (both stored dimension-for-dimension; the 4-D indexing's real meaning isn't confirmed).
            for (var tier = 0; tier < n.SkillInfo1.Length; tier++)
            for (var slot = 0; slot < n.SkillInfo1[tier].Length; slot++)
            {
                var skillId = n.SkillInfo1[tier][slot];
                if (skillId == 0) continue;
                skillRows.Add(string.Join(", ",
                    SqlSeedWriter.Number(n.Index), "1", tier.ToString(), "NULL", "NULL", slot.ToString(),
                    skillId.ToString()));
            }

            for (var a = 0; a < 3; a++)
            for (var b = 0; b < 3; b++)
            for (var c = 0; c < 3; c++)
            for (var slot = 0; slot < 8; slot++)
            {
                var skillId = n.SkillInfo2[a * 72 + b * 24 + c * 8 + slot];
                if (skillId == 0) continue;
                skillRows.Add(string.Join(", ",
                    SqlSeedWriter.Number(n.Index), "2", a.ToString(), b.ToString(), c.ToString(), slot.ToString(),
                    skillId.ToString()));
            }

            for (var row = 0; row < n.GambleCostInfo.Length; row++)
            for (var col = 0; col < n.GambleCostInfo[row].Length; col++)
            {
                var value = n.GambleCostInfo[row][col];
                if (value == 0) continue;
                gambleRows.Add(string.Join(", ",
                    SqlSeedWriter.Number(n.Index), row.ToString(), col.ToString(), value.ToString()));
            }
        }

        var sb = new StringBuilder();
        sb.Append("-- Generated by Tools/Fenrir.Tools.LegacyDataImport/Legacy/Seeding/NpcSeedGenerator.cs\n");
        sb.Append("-- from Server/BuildEU33/DATA (005_00005.IMG). Regenerate rather than hand-edit.\n");
        sb.Append("-- One row per NPC_INFO record with Index != 0 (131 of 500 legacy array slots); every\n");
        sb.Append("-- fixed-size sub-array is normalized into its own child table, one row per populated\n");
        sb.Append("-- slot -- see each world.Npc*.sql table's own header for the per-table rationale and the\n");
        sb.Append("-- Menu/GambleCostInfo density surprises. All six tables are seeded together, guarded by\n");
        sb.Append("-- a single check against world.Npcs, since they are always populated from the same run.\n");
        sb.Append("IF NOT EXISTS (SELECT 1 FROM world.Npcs)\n");
        sb.Append("BEGIN\n");

        SqlSeedWriter.WriteInserts(sb, "world.Npcs", NpcColumns, npcRows);
        SqlSeedWriter.WriteInserts(sb, "world.NpcSpeeches", SpeechColumns, speechRows);
        SqlSeedWriter.WriteInserts(sb, "world.NpcMenuOptions", MenuColumns, menuRows);
        SqlSeedWriter.WriteInserts(sb, "world.NpcShopItems", ShopColumns, shopRows);
        SqlSeedWriter.WriteInserts(sb, "world.NpcSkillOffers", SkillOfferColumns, skillRows);
        SqlSeedWriter.WriteInserts(sb, "world.NpcGambleCosts", GambleColumns, gambleRows);

        sb.Append("END;\n");

        File.WriteAllText(outputPath, sb.ToString());
        Console.WriteLine($"Npcs.sql: {npcRows.Count} NPCs, {speechRows.Count} speech lines, " +
                          $"{menuRows.Count} menu slots, {shopRows.Count} shop slots, " +
                          $"{skillRows.Count} skill offers, {gambleRows.Count} gamble-cost cells -> {outputPath}");
    }
}
