using Fenrir.Tools.LegacyDataImport.Legacy.Readers;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Validation;

/// <summary>Ad-hoc console check for <see cref="MonsterSpawnRegionReader" />: file/row counts and a few sample rows.</summary>
internal static class MonsterSpawnRegionValidation
{
    public static void Run(string dataDir)
    {
        var summonDirectory = Path.Combine(dataDir, "SUMMON");
        var records = MonsterSpawnRegionReader.ReadAll(summonDirectory, out var fileCount, out var skippedLineCount);

        Console.WriteLine(
            $"[MonsterSpawnRegionValidation] '*.WREGION.csv' files found under '{summonDirectory}' (recursive): {fileCount}");
        Console.WriteLine($"[MonsterSpawnRegionValidation] Rows parsed: {records.Count}");
        Console.WriteLine($"[MonsterSpawnRegionValidation] Rows skipped as malformed: {skippedLineCount}");

        Console.WriteLine("[MonsterSpawnRegionValidation] First 5 parsed rows:");
        foreach (var record in records.Take(5))
            Console.WriteLine(
                $"  Zone={record.ZoneNumber,-4} Source={record.SourceFileName,-34} " +
                $"Value01={record.Value01} MonsterIndex={record.MonsterIndex,-6} Value03={record.Value03} " +
                $"Number={record.Number,-4} Location=[{record.Location[0]},{record.Location[1]},{record.Location[2]}] " +
                $"Radius={record.Radius}");
    }
}
