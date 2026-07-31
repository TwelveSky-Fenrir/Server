using Fenrir.Tools.DbMigrator.Legacy.Records;

namespace Fenrir.Tools.DbMigrator.Legacy.Readers;

internal static class ZoneNpcPlacementReader
{
    private const string FileName = "002.BIN";
    private const int RecordCount = 350;
    private const int RecordSize = 2004;
    private const int NpcSlotCount = 100;

    public static IReadOnlyList<ZoneNpcPlacementRecord> ReadAll(string dataDirectory)
    {
        var filePath = Path.Combine(dataDirectory, FileName);
        var bytes = File.ReadAllBytes(filePath);

        var expectedLength = RecordCount * RecordSize;
        if (bytes.Length != expectedLength)
            throw new InvalidDataException(
                $"'{FileName}': file size {bytes.Length} does not match the expected {expectedLength} bytes " +
                $"({RecordCount} records * {RecordSize} bytes) -- record layout or count is wrong for this build.");

        var zones = new List<ZoneNpcPlacementRecord>(RecordCount);
        for (var i = 0; i < RecordCount; i++)
            zones.Add(ReadOne(bytes.AsSpan(i * RecordSize, RecordSize), i + 1));

        return zones;
    }

    private static ZoneNpcPlacementRecord ReadOne(ReadOnlySpan<byte> record, int zoneNumber)
    {
        var reader = new LegacySpanReader(record);

        var totalNpcNum = reader.ReadInt32();
        var npcNumber = reader.ReadInt32Array(NpcSlotCount);

        var npcCoord = new (float X, float Y, float Z)[NpcSlotCount];
        for (var i = 0; i < NpcSlotCount; i++)
            npcCoord[i] = (reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        var npcAngle = reader.ReadSingleArray(NpcSlotCount);

        return new ZoneNpcPlacementRecord(zoneNumber, totalNpcNum, npcNumber, npcCoord, npcAngle);
    }
}
