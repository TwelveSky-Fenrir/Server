using Fenrir.Tools.LegacyDataImport.Legacy.Validation;

// One-off migration/verification tool (not part of any deployed runtime): decodes every legacy BuildEU33
// static-data source (item/skill/monster/npc/quest/level/gemsocket .IMG tables, 002.BIN/003.BIN zone reference
// data, and the .WREGION.csv monster-spawn-region files) and cross-validates each against whatever legacy CSV
// exports/invariants exist, as ground truth before the SQL Server schema/seed phase consumes this same reader
// code. Usage: dotnet run --project Tools/Fenrir.Tools.LegacyDataImport -- <path-to-BuildEU33-DATA-dir>

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: Fenrir.Tools.LegacyDataImport <path-to-BuildEU33-DATA-dir>");
    return 1;
}

var dataDirectory = args[0];
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
