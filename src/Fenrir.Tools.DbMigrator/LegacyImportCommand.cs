using Fenrir.Tools.DbMigrator.Legacy.Seeding;
using Fenrir.Tools.DbMigrator.Legacy.Validation;

namespace Fenrir.Tools.DbMigrator;

public static class LegacyImportCommand
{
    public const string Keyword = "import-legacy-data";

    public static void PrintUsage()
    {
        Console.Error.WriteLine(
            $"usage: Fenrir.Tools.DbMigrator {Keyword} <path-to-BuildEU33-DATA-dir> [--regenerate-seed <path-to-Database/Migrations/Seed/world>]");
    }

    public static int Run(string[] commandArgs)
    {
        if (commandArgs.Length < 1)
        {
            PrintUsage();
            return 1;
        }

        var dataDirectory = commandArgs[0];

        if (commandArgs.Length >= 3 && commandArgs[1] == "--regenerate-seed")
        {
            var seedWorldDirectory = commandArgs[2];
            ItemSeedGenerator.Generate(dataDirectory, Path.Combine(seedWorldDirectory, "005_items.sql"));
            MonsterSeedGenerator.Generate(dataDirectory, Path.Combine(seedWorldDirectory, "015_monsters.sql"));
            GemSocketSeedGenerator.Generate(dataDirectory, Path.Combine(seedWorldDirectory, "021_gem_sockets.sql"));
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
    }
}
