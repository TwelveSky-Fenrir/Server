using System.Text;
using Fenrir.Tools.DbMigrator.Legacy.Readers;

namespace Fenrir.Tools.DbMigrator.Legacy.Validation;

internal static class NpcValidation
{
    public static void Run(string dataDir)
    {
        var npcs = NpcReader.ReadAllRaw(dataDir);
        Console.WriteLine($"Parsed {npcs.Count} NPC records from 005_00005.IMG.");

        Console.WriteLine("First 5 NPC names (Index | Name):");
        foreach (var npc in npcs.Take(5))
            Console.WriteLine($"  [{npc.Index}] \"{npc.Name}\"");

        Console.WriteLine();
        Console.WriteLine("Low-confidence spot-check: first 5 rows' nMenu[0..9] and nShopInfo[0][0..9] from our " +
                          "parse, next to the corresponding CSV rows (different locale/version -- do NOT treat as ground truth):");
        var menuCsvRows = TryReadCsvRows(Path.Combine(dataDir, "NpcMenu.CSV"));
        var shopCsvRows = TryReadCsvRows(Path.Combine(dataDir, "NpcShop.CSV"));

        for (var i = 0; i < 5 && i < npcs.Count; i++)
        {
            var npc = npcs[i];
            Console.WriteLine($"  NPC[{npc.Index}] \"{npc.Name}\"");
            Console.WriteLine($"    parsed nMenu[0..9]      = [{string.Join(", ", npc.Menu.Take(10))}]");
            if (menuCsvRows.TryGetValue(npc.Index, out var menuRow))
                Console.WriteLine($"    NpcMenu.CSV row fields  = [{string.Join(", ", menuRow.Skip(2).Take(10))}]");

            Console.WriteLine($"    parsed nShopInfo[0][0..9] = [{string.Join(", ", npc.ShopInfo[0].Take(10))}]");
            if (shopCsvRows.TryGetValue(npc.Index, out var shopRow))
                Console.WriteLine($"    NpcShop.CSV row fields    = [{string.Join(", ", shopRow.Skip(2).Take(10))}]");
        }
    }

    private static Dictionary<int, string[]> TryReadCsvRows(string csvPath)
    {
        var rows = new Dictionary<int, string[]>();
        if (!File.Exists(csvPath)) return rows;

        foreach (var line in File.ReadLines(csvPath, Encoding.Latin1))
        {
            var fields = line.Split('|');
            if (fields.Length == 0 || !int.TryParse(fields[0], out var index)) continue;
            rows[index] = fields;
        }

        return rows;
    }
}
