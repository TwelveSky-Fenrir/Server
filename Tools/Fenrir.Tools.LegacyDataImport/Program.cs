using Fenrir.Tools.LegacyDataImport.Legacy.Seeding;
using Fenrir.Tools.LegacyDataImport.Legacy.Validation;

if (args.Length < 1)
{
    Console.Error.WriteLine(
        "usage: Fenrir.Tools.LegacyDataImport <path-to-BuildEU33-DATA-dir> [--regenerate-seed <path-to-Database/Migrations/Seed/world>]");
    return 1;
}

var dataDirectory = args[0];

if (args.Length >= 3 && args[1] == "--regenerate-seed")
{
    var seedWorldDirectory = args[2];
    ItemSeedGenerator.Generate(dataDirectory, Path.Combine(seedWorldDirectory, "080_items.sql"));
    MonsterSeedGenerator.Generate(dataDirectory, Path.Combine(seedWorldDirectory, "090_monsters.sql"));
    return 0;
}

var failures = 0;

RunSection("Item", () => ItemValidation.Run(dataDirectory));
RunSection("Skill", () => SkillValidation.Run(dataDirectory));
RunSection("Monster", () => MonsterValidation.Run(dataDirectory));
RunSection("Npc", () => NpcValidation.Run(dataDirectory));
RunSection("Quest/Level/Socket", () => QuestLevelSocketValidation.Run(dataDirectory));
RunSection("Zone reference data (002.BIN/003.BIN)", () => ZoneBinsValidation.Run(dataDirectory));
RunSection("Monster spawn regions (.WREGION.csv)", () => MonsterSpawnRegionValidation.Run(dataDirectory));

Console.WriteLine();
Console.WriteLine(failures == 0
    ? "=== All sections completed without an exception ==="
    : $"=== {failures} section(s) threw ===");
return failures == 0 ? 0 : 1;

void RunSection(string name, Action run)
{
    Console.WriteLine();
    Console.WriteLine($"--- {name} ---");
    try
    {
        run();
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"[{name}] threw: {ex}");
    }
}
