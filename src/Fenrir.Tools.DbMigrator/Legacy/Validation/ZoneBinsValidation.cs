using Fenrir.Tools.DbMigrator.Legacy.Readers;

namespace Fenrir.Tools.DbMigrator.Legacy.Validation;

internal static class ZoneBinsValidation
{
    private const int ExpectedZoneCount = 350;
    private const int NpcRecordSize = 2004;
    private const int MoveRecordSize = 3220;

    public static void Run(string dataDir)
    {
        Console.WriteLine("=== ZoneBins validation (002.BIN / 003.BIN) ===");

        CheckFileSize(Path.Combine(dataDir, "002.BIN"), ExpectedZoneCount * NpcRecordSize, "002.BIN (ZONENPCINFODATA)");
        CheckFileSize(Path.Combine(dataDir, "003.BIN"), ExpectedZoneCount * MoveRecordSize, "003.BIN (ZONEMOVEDATA)");

        var npcZones = ZoneNpcPlacementReader.ReadAll(dataDir);
        var moveZones = ZoneMoveDataReader.ReadAll(dataDir);

        Console.WriteLine($"ZoneNpcPlacement records parsed: {npcZones.Count} (expected {ExpectedZoneCount}) -- " +
                          $"{(npcZones.Count == ExpectedZoneCount ? "OK" : "MISMATCH")}");
        Console.WriteLine($"ZoneMoveData records parsed: {moveZones.Count} (expected {ExpectedZoneCount}) -- " +
                          $"{(moveZones.Count == ExpectedZoneCount ? "OK" : "MISMATCH")}");

        var populatedZoneNumbers = DiscoverPopulatedZoneNumbers(dataDir);
        Console.WriteLine(
            $"Discovered {populatedZoneNumbers.Count} zone(s) with a DATA/WORLD/Z0NN.WM file in this build.");

        var sampleLive = populatedZoneNumbers.Take(5).ToList();
        var sampleUnused = Enumerable.Range(1, ExpectedZoneCount)
            .Where(z => !populatedZoneNumbers.Contains(z))
            .Take(3)
            .ToList();
        var sampleZones = sampleLive.Concat(sampleUnused).ToList();

        Console.WriteLine();
        Console.WriteLine("-- ZoneNpcPlacement (002.BIN) --");
        foreach (var zoneNumber in sampleZones)
        {
            var zone = npcZones[zoneNumber - 1];
            var label = populatedZoneNumbers.Contains(zoneNumber) ? "LIVE" : "unused";
            var firstNpc = zone.TotalNpcNum > 0 ? zone.NpcNumber[0] : 0;
            var firstCoord = zone.TotalNpcNum > 0 ? zone.NpcCoord[0] : (X: 0f, Y: 0f, Z: 0f);
            Console.WriteLine(
                $"  Zone {zoneNumber,3} [{label,-6}] TotalNpcNum={zone.TotalNpcNum,3} " +
                $"NpcNumber[0]={firstNpc,-6} NpcCoord[0]=({firstCoord.X:F1}, {firstCoord.Y:F1}, {firstCoord.Z:F1})");
        }

        Console.WriteLine();
        Console.WriteLine("-- ZoneMoveData (003.BIN) --");
        foreach (var zoneNumber in sampleZones)
        {
            var zone = moveZones[zoneNumber - 1];
            var label = populatedZoneNumbers.Contains(zoneNumber) ? "LIVE" : "unused";
            Console.WriteLine(
                $"  Zone {zoneNumber,3} [{label,-6}] FirstCoord=({zone.FirstCoord.X:F1}, {zone.FirstCoord.Y:F1}, {zone.FirstCoord.Z:F1}) " +
                $"NextZoneNum={zone.NextZoneNum}");
        }
    }

    private static void CheckFileSize(string path, int expectedBytes, string label)
    {
        var actualBytes = new FileInfo(path).Length;
        var status = actualBytes == expectedBytes ? "OK" : "MISMATCH";
        Console.WriteLine($"{label}: {actualBytes} bytes (expected {expectedBytes}) -- {status}");
    }

    private static List<int> DiscoverPopulatedZoneNumbers(string dataDir)
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
}
