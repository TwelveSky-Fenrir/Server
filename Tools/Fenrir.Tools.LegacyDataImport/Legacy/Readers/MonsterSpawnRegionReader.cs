using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Readers;

/// <summary>
///     Parses every <c>*.WREGION.csv</c> file (Header/Protocol/STRUCT.h:1327-1335's <c>WORLD_REGION_INFO</c>,
///     pipe-delimited, no header row) found under a <c>DATA/SUMMON</c> directory tree. Confirmed by prior
///     investigation that the legacy server (Server/ts25zone/S10_MySummon.cpp) reads only these <c>.csv</c> text
///     files at runtime; the sibling extensionless <c>.WREGION</c> binaries (found under a <c>WRegion/</c>
///     subfolder in this build) are dead/unused code and are never opened by the reader. Real data has every
///     file directly under <c>DATA/SUMMON/</c> (none under <c>DATA/SUMMON/WRegion/</c>), but this reader searches
///     the whole subtree rather than hardcoding that so it stays correct if that ever changes.
/// </summary>
internal static class MonsterSpawnRegionReader
{
    private const string SearchPattern = "*.WREGION.csv";
    private const int ExpectedFieldCount = 8;

    // Filenames look like "Z001_SUMMONMONSTER.WREGION.csv", "Z019_SUMMONMONSTER_3.WREGION.csv", or
    // "Z040_SUMMONBOSSMONSTER.WREGION.csv" -- only the leading zone number is parsed into its own field here;
    // the rest (normal/boss/numbered-variant/"_FIX"/"_M") is left embedded in SourceFileName for a later,
    // SQL-schema-design phase to normalize, per instructions not to make normalization decisions yet.
    private static readonly Regex ZoneNumberPattern = new(@"^Z(\d+)_", RegexOptions.Compiled);

    /// <summary>Raw parse of every row in every matching file -- no per-load patches applied.</summary>
    public static IReadOnlyList<MonsterSpawnRegionRecord> ReadAllRaw(string summonDirectory)
    {
        return ReadAllRaw(summonDirectory, out _, out _);
    }

    /// <summary>
    ///     Same as <see cref="ReadAllRaw(string)" />, plus diagnostics: <paramref name="fileCount" /> is how many
    ///     <c>*.WREGION.csv</c> files were found, and <paramref name="skippedLineCount" /> is how many lines
    ///     across all of them failed to parse as exactly 8 int columns and were skipped (logged to stderr).
    /// </summary>
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
                    continue; // blank trailing line -- not a malformed data row, just nothing to parse

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

    /// <summary>
    ///     No per-load patches are known to apply to <c>WORLD_REGION_INFO</c> rows (unlike e.g. item data --
    ///     see <c>ItemReader</c> -- <c>S10_MySummon.cpp</c> reads these CSV rows directly into the runtime spawn
    ///     list with no further transform), so this is identical to <see cref="ReadAllRaw(string)" />.
    /// </summary>
    public static IReadOnlyList<MonsterSpawnRegionRecord> ReadAll(string summonDirectory)
    {
        return ReadAllRaw(summonDirectory);
    }

    /// <summary>
    ///     Diagnostics-returning overload of <see cref="ReadAll(string)" />; see
    ///     <see cref="ReadAllRaw(string, out int, out int)" />.
    /// </summary>
    public static IReadOnlyList<MonsterSpawnRegionRecord> ReadAll(string summonDirectory, out int fileCount,
        out int skippedLineCount)
    {
        return ReadAllRaw(summonDirectory, out fileCount, out skippedLineCount);
    }

    /// <summary>
    ///     Splits a line on '|' into exactly 8 int columns. Real files terminate every data line with a trailing
    ///     '|' before the CRLF (an artifact of the legacy writer, <c>MySummonToFile</c>), which produces a 9th,
    ///     empty trailing field on split -- that specific shape is tolerated; anything else (wrong field count,
    ///     non-numeric field, e.g. from an embedded pipe character corrupting the split) is rejected defensively
    ///     rather than crashing.
    /// </summary>
    private static bool TryParseLine(string line, out int[] values)
    {
        values = [];
        var fields = line.Split('|');

        if (fields.Length == ExpectedFieldCount + 1 && fields[ExpectedFieldCount].Length == 0)
            fields = fields[..ExpectedFieldCount]; // drop the trailing empty field from the writer's trailing '|'

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
