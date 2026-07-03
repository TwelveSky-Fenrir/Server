using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Readers;

/// <summary>
///     Parses <c>002.BIN</c> (Header/Protocol/STRUCT.h:1380-1386, <c>ZONENPCINFODATA</c>): a raw array of
///     <c>MAX_ZONE_NUMBER_NUM</c> (350) fixed-size structs written back-to-back with no header, no zlib, and no
///     XOR obfuscation -- unlike the <c>.IMG</c> datasets, this is a plain memory dump straight to disk.
///     No per-load patches are known to apply to this file, so there is only one read method.
/// </summary>
internal static class ZoneNpcPlacementReader
{
    private const string FileName = "002.BIN";
    private const int RecordCount = 350; // MAX_ZONE_NUMBER_NUM
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
