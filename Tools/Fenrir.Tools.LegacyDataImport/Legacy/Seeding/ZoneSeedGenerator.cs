using System.Globalization;
using System.Text;
using Fenrir.Tools.LegacyDataImport.Legacy.Readers;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Seeding;

/// <summary>
///     Generates the five <c>world.*</c> zone-topology seed scripts (Zones, ZoneNpcSpawns, ZonePortals,
///     ZoneSpawnPoints, MonsterSpawnRegions) from <c>002.BIN</c>/<c>003.BIN</c>/<c>*.WREGION.csv</c>, restricted
///     to the ~117 zone numbers "live" in this build (have a matching <c>DATA/WORLD/Z0NN.WM</c> file).
/// </summary>
internal static class ZoneSeedGenerator
{
    private const int ChunkSize = 500; // SQL Server hard cap is 1000 rows/VALUES list; 500 gives headroom.

    /// <summary>
    ///     Reads <paramref name="dataDir" /> (Server/BuildEU33/DATA layout) and writes the five seed .sql files into
    ///     <paramref name="outputDir" />.
    /// </summary>
    public static string Generate(string dataDir, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        var liveZoneNumbers = DiscoverLiveZoneNumbers(dataDir);
        var liveZoneSet = liveZoneNumbers.ToHashSet();

        var npcZones = ZoneNpcPlacementReader.ReadAll(dataDir);
        var moveZones = ZoneMoveDataReader.ReadAll(dataDir);
        var spawnRegions = MonsterSpawnRegionReader.ReadAll(Path.Combine(dataDir, "SUMMON"), out var regionFileCount,
            out var regionSkippedLines);

        var report = new StringBuilder();
        report.AppendLine("=== ZoneSeedGenerator ===");
        report.AppendLine($"Live zones (have DATA/WORLD/Z0NN.WM): {liveZoneNumbers.Count} / 350 array slots.");

        var zoneRows = new List<string[]>();
        foreach (var zoneNumber in liveZoneNumbers)
        {
            var move = moveZones[zoneNumber - 1];
            zoneRows.Add(
            [
                zoneNumber.ToString(CultureInfo.InvariantCulture),
                FormatReal(move.FirstCoord.X),
                FormatReal(move.FirstCoord.Y),
                FormatReal(move.FirstCoord.Z)
            ]);
        }

        WriteSeedFile(
            Path.Combine(outputDir, "020_zones.sql"),
            "world.Zones",
            "-- Seeds world.Zones from 002.BIN/003.BIN: one row per zone number with a DATA/WORLD/Z0NN.WM file\n" +
            "-- in this build (117 of the legacy 350-slot array) -- see 30_tables/world/Zones.sql for why.",
            ["ZoneNumber", "DefaultSpawnX", "DefaultSpawnY", "DefaultSpawnZ"],
            zoneRows);
        report.AppendLine($"world.Zones: {zoneRows.Count} rows (theoretical max 350).");

        var npcSpawnRows = new List<string[]>();
        long npcTheoreticalMax = 0;
        foreach (var zoneNumber in liveZoneNumbers)
        {
            var npc = npcZones[zoneNumber - 1];
            npcTheoreticalMax += 100;
            var count = Math.Clamp(npc.TotalNpcNum, 0, 100);
            for (var slot = 0; slot < count; slot++)
            {
                var npcId = npc.NpcNumber[slot];
                var coord = npc.NpcCoord[slot];
                npcSpawnRows.Add(
                [
                    zoneNumber.ToString(CultureInfo.InvariantCulture),
                    slot.ToString(CultureInfo.InvariantCulture),
                    FormatIntOrNull(npcId == 0 ? null : npcId),
                    FormatReal(coord.X),
                    FormatReal(coord.Y),
                    FormatReal(coord.Z),
                    FormatReal(npc.NpcAngle[slot])
                ]);
            }
        }

        WriteSeedFile(
            Path.Combine(outputDir, "021_zone_npc_spawns.sql"),
            "world.ZoneNpcSpawns",
            "-- Seeds world.ZoneNpcSpawns from 002.BIN: one row per populated NPC-placement slot (291 of a\n" +
            "-- 11700-slot ceiling across the 117 live zones) -- see 30_tables/world/ZoneNpcSpawns.sql.\n" +
            "-- MUST run after both the world.Zones and world.Npcs seed scripts (FK dependency).",
            ["ZoneNumber", "SlotIndex", "NpcId", "PosX", "PosY", "PosZ", "Angle"],
            npcSpawnRows);
        report.AppendLine(
            $"world.ZoneNpcSpawns: {npcSpawnRows.Count} rows (theoretical max {npcTheoreticalMax} = {liveZoneNumbers.Count} live zones * 100 slots; " +
            $"full legacy array would be 350 * 100 = 35000).");

        var portalRows = new List<string[]>();
        var spawnPointRows = new List<string[]>();
        long portalTheoreticalMax = 0;
        long spawnPointTheoreticalMax = 0;
        var portalTargetsOutsideLiveSet = 0;
        var spawnPointSourcesOutsideLiveSet = 0;

        foreach (var zoneNumber in liveZoneNumbers)
        {
            var move = moveZones[zoneNumber - 1];
            portalTheoreticalMax += 100;
            spawnPointTheoreticalMax += 100;

            var portalCount = Math.Clamp(move.NextZoneNum, 0, 100);
            for (var slot = 0; slot < portalCount; slot++)
            {
                var target = move.NextZone[slot];
                int? targetOrNull = target;
                if (target == 0)
                {
                    targetOrNull = null;
                }
                else if (!liveZoneSet.Contains(target))
                {
                    targetOrNull = null;
                    portalTargetsOutsideLiveSet++;
                }

                var xyz = move.Xyz[slot];
                portalRows.Add(
                [
                    zoneNumber.ToString(CultureInfo.InvariantCulture),
                    slot.ToString(CultureInfo.InvariantCulture),
                    FormatReal(xyz.X),
                    FormatReal(xyz.Y),
                    FormatReal(xyz.Z),
                    FormatIntOrNull(targetOrNull)
                ]);
            }

            var spawnPointCount = Math.Clamp(move.StartCoordNum, 0, 100);
            for (var slot = 0; slot < spawnPointCount; slot++)
            {
                var fromZone = move.StartCoordZone[slot];
                int? fromZoneOrNull = fromZone;
                if (fromZone == 0)
                {
                    fromZoneOrNull = null;
                }
                else if (!liveZoneSet.Contains(fromZone))
                {
                    fromZoneOrNull = null;
                    spawnPointSourcesOutsideLiveSet++;
                }

                var coord = move.StartCoord[slot];
                spawnPointRows.Add(
                [
                    zoneNumber.ToString(CultureInfo.InvariantCulture),
                    slot.ToString(CultureInfo.InvariantCulture),
                    FormatIntOrNull(fromZoneOrNull),
                    FormatReal(coord.X),
                    FormatReal(coord.Y),
                    FormatReal(coord.Z)
                ]);
            }
        }

        WriteSeedFile(
            Path.Combine(outputDir, "022_zone_portals.sql"),
            "world.ZonePortals",
            "-- Seeds world.ZonePortals from 003.BIN: one row per populated outbound-portal slot (413 of a\n" +
            "-- 11700-slot ceiling) -- see 30_tables/world/ZonePortals.sql for the TargetZoneNumber NULL rules.\n" +
            "-- MUST run after the world.Zones seed script (FK dependency).",
            ["ZoneNumber", "SlotIndex", "TriggerX", "TriggerY", "TriggerZ", "TargetZoneNumber"],
            portalRows);
        report.AppendLine(
            $"world.ZonePortals: {portalRows.Count} rows (theoretical max {portalTheoreticalMax}; full legacy array 35000). " +
            $"TargetZoneNumber nulled for out-of-live-set reference: {portalTargetsOutsideLiveSet}.");

        WriteSeedFile(
            Path.Combine(outputDir, "023_zone_spawn_points.sql"),
            "world.ZoneSpawnPoints",
            "-- Seeds world.ZoneSpawnPoints from 003.BIN: one row per populated inbound-landing slot (413 of a\n" +
            "-- 11700-slot ceiling) -- see 30_tables/world/ZoneSpawnPoints.sql for the FromZoneNumber NULL rules.\n" +
            "-- MUST run after the world.Zones seed script (FK dependency).",
            ["ZoneNumber", "SlotIndex", "FromZoneNumber", "PosX", "PosY", "PosZ"],
            spawnPointRows);
        report.AppendLine(
            $"world.ZoneSpawnPoints: {spawnPointRows.Count} rows (theoretical max {spawnPointTheoreticalMax}; full legacy array 35000). " +
            $"FromZoneNumber nulled for out-of-live-set reference: {spawnPointSourcesOutsideLiveSet}.");

        // ~49% of *.WREGION.csv region rows reference a non-live zone -- ZoneNumber is nullable (not a
        // NOT NULL FK) so SourceFileName preserves the raw zone number as a lossless fallback.
        var regionRows = new List<string[]>();
        var regionZonesOutsideLiveSet = 0;
        foreach (var region in spawnRegions)
        {
            int? zoneNumberOrNull = region.ZoneNumber;
            if (!liveZoneSet.Contains(region.ZoneNumber))
            {
                zoneNumberOrNull = null;
                regionZonesOutsideLiveSet++;
            }

            regionRows.Add(
            [
                FormatIntOrNull(zoneNumberOrNull),
                FormatString(region.SourceFileName),
                region.Value01.ToString(CultureInfo.InvariantCulture),
                FormatIntOrNull(region.MonsterIndex == 0 ? null : region.MonsterIndex),
                region.Value03.ToString(CultureInfo.InvariantCulture),
                region.Number.ToString(CultureInfo.InvariantCulture),
                region.Location[0].ToString(CultureInfo.InvariantCulture),
                region.Location[1].ToString(CultureInfo.InvariantCulture),
                region.Location[2].ToString(CultureInfo.InvariantCulture),
                region.Radius.ToString(CultureInfo.InvariantCulture)
            ]);
        }

        WriteSeedFile(
            Path.Combine(outputDir, "024_monster_spawn_regions.sql"),
            "world.MonsterSpawnRegions",
            "-- Seeds world.MonsterSpawnRegions from every DATA/SUMMON/*.WREGION.csv row (21960 rows, already\n" +
            "-- one row per source line) -- see 30_tables/world/MonsterSpawnRegions.sql for the ZoneNumber NULL\n" +
            "-- deviation (~49% of rows name a zone this build never shipped).\n" +
            "-- MUST run after both the world.Zones and world.Monsters seed scripts (FK dependency).",
            [
                "ZoneNumber", "SourceFileName", "Value01", "MonsterId", "Value03", "Number", "LocationX", "LocationY",
                "LocationZ", "Radius"
            ],
            regionRows);
        report.AppendLine(
            $"world.MonsterSpawnRegions: {regionRows.Count} rows from {regionFileCount} *.WREGION.csv files " +
            $"({regionSkippedLines} malformed lines skipped by the reader). " +
            $"ZoneNumber nulled for out-of-live-set reference: {regionZonesOutsideLiveSet}.");

        var totalNormalizedRows = zoneRows.Count + npcSpawnRows.Count + portalRows.Count + spawnPointRows.Count +
                                  regionRows.Count;
        var totalTheoreticalMax =
            350 + 35000 + 35000 + 35000 + regionRows.Count; // regions have no fixed-array analogue
        report.AppendLine(
            $"TOTAL seeded rows across all 5 tables: {totalNormalizedRows} vs. theoretical fixed-array max ~{totalTheoreticalMax} " +
            "(350 zones * 100 slots * 3 sparse tables + Zones itself; MonsterSpawnRegions has no fixed-array analogue to compare against).");

        return report.ToString();
    }

    private static List<int> DiscoverLiveZoneNumbers(string dataDir)
    {
        var worldDir = Path.Combine(dataDir, "WORLD");
        var zoneNumbers = new List<int>();
        if (!Directory.Exists(worldDir)) return zoneNumbers;

        foreach (var path in Directory.EnumerateFiles(worldDir, "Z*.WM"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (name.Length > 1 && int.TryParse(name.AsSpan(1), out var zoneNumber))
                zoneNumbers.Add(zoneNumber);
        }

        zoneNumbers.Sort();
        return zoneNumbers;
    }

    private static void WriteSeedFile(string path, string tableName, string headerComment, string[] columns,
        List<string[]> rows)
    {
        var sql = new StringBuilder();
        sql.AppendLine(headerComment);
        sql.AppendLine($"IF NOT EXISTS (SELECT 1 FROM {tableName})");
        sql.AppendLine("BEGIN");

        if (rows.Count == 0)
        {
            sql.AppendLine("    -- No rows to seed for this table in this build.");
        }
        else
        {
            var columnList = string.Join(", ", columns);
            for (var chunkStart = 0; chunkStart < rows.Count; chunkStart += ChunkSize)
            {
                var chunk = rows.Skip(chunkStart).Take(ChunkSize).ToList();
                sql.AppendLine($"    INSERT INTO {tableName} ({columnList}) VALUES");
                for (var i = 0; i < chunk.Count; i++)
                {
                    var isLast = i == chunk.Count - 1;
                    sql.AppendLine($"    ({string.Join(", ", chunk[i])}){(isLast ? "" : ",")}");
                }

                sql.AppendLine("    ;");
            }
        }

        sql.AppendLine("END;");

        File.WriteAllText(path, sql.ToString());
    }

    private static string FormatReal(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new InvalidDataException(
                $"Encountered non-finite float value {value} while generating zone seed data -- source data assumption violated.");

        return value.ToString("G9", CultureInfo.InvariantCulture);
    }

    private static string FormatIntOrNull(int? value)
    {
        return value is null ? "NULL" : value.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatString(string value)
    {
        return "N'" + value.Replace("'", "''") + "'";
    }
}
