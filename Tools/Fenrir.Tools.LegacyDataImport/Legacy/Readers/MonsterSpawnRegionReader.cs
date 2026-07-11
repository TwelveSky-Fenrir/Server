using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Readers;

internal static class MonsterSpawnRegionReader
{
    private const string SearchPattern = "*.WREGION.csv";
    private const int ExpectedFieldCount = 8;

    private static readonly Regex ZoneNumberPattern = new(@"^Z(\d+)_", RegexOptions.Compiled);

    public static IReadOnlyList<MonsterSpawnRegionRecord> ReadAllRaw(string summonDirectory)
    {
        return ReadAllRaw(summonDirectory, out _, out _);
    }

    public static IReadOnlyList<MonsterSpawnRegionRecord> ReadAllRaw(string summonDirectory, out int fileCount,
        out int skippedLineCount)
    {
        var files = Directory.EnumerateFiles(summonDirectory, SearchPattern, SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
        fileCount = files.Count;

        var records = new List<MonsterSpawnRegionRecord>();
        var skipped = 0;

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileName(filePath);
            var zoneMatch = ZoneNumberPattern.Match(fileName);
            if (!zoneMatch.Success)
            {
                Console.Error.WriteLine(
                    $"[MonsterSpawnRegionReader] '{fileName}': could not parse a leading \"Z0NN_\" zone number, skipping entire file.");
                continue;
            }

            var zoneNumber = int.Parse(zoneMatch.Groups[1].Value, CultureInfo.InvariantCulture);

            var lineNumber = 0;
            foreach (var line in File.ReadLines(filePath, Encoding.Latin1))
            {
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!TryParseLine(line, out var values))
                {
                    skipped++;
                    Console.Error.WriteLine(
                        $"[MonsterSpawnRegionReader] '{fileName}' line {lineNumber}: expected 8 pipe-delimited int fields, skipping: \"{line}\"");
                    continue;
                }

                records.Add(new MonsterSpawnRegionRecord(
                    zoneNumber,
                    fileName,
                    values[0],
                    values[1],
                    values[2],
                    values[3],
                    [values[4], values[5], values[6]],
                    values[7]));
            }
        }

        skippedLineCount = skipped;
        return records;
    }

    public static IReadOnlyList<MonsterSpawnRegionRecord> ReadAll(string summonDirectory)
    {
        return ReadAllRaw(summonDirectory);
    }

    public static IReadOnlyList<MonsterSpawnRegionRecord> ReadAll(string summonDirectory, out int fileCount,
        out int skippedLineCount)
    {
        return ReadAllRaw(summonDirectory, out fileCount, out skippedLineCount);
    }

    private static bool TryParseLine(string line, out int[] values)
    {
        values = [];
        var fields = line.Split('|');

        if (fields.Length == ExpectedFieldCount + 1 && fields[ExpectedFieldCount].Length == 0)
            fields = fields[..ExpectedFieldCount];

        if (fields.Length != ExpectedFieldCount)
            return false;

        var parsed = new int[ExpectedFieldCount];
        for (var i = 0; i < ExpectedFieldCount; i++)
            if (!int.TryParse(fields[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed[i]))
                return false;

        values = parsed;
        return true;
    }
}
