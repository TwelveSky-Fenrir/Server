using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Readers;

internal static class ZoneMoveDataReader
{
    private const string FileName = "003.BIN";
    private const int RecordCount = 350;
    private const int RecordSize = 3220;
    private const int SlotCount = 100;

    public static IReadOnlyList<ZoneMoveDataRecord> ReadAll(string dataDirectory)
    {
        var filePath = Path.Combine(dataDirectory, FileName);
        var bytes = File.ReadAllBytes(filePath);

        var expectedLength = RecordCount * RecordSize;
        if (bytes.Length != expectedLength)
            throw new InvalidDataException(
                $"'{FileName}': file size {bytes.Length} does not match the expected {expectedLength} bytes " +
                $"({RecordCount} records * {RecordSize} bytes) -- record layout or count is wrong for this build.");

        var zones = new List<ZoneMoveDataRecord>(RecordCount);
        for (var i = 0; i < RecordCount; i++)
            zones.Add(ReadOne(bytes.AsSpan(i * RecordSize, RecordSize), i + 1));

        return zones;
    }

    private static ZoneMoveDataRecord ReadOne(ReadOnlySpan<byte> record, int zoneNumber)
    {
        var reader = new LegacySpanReader(record);

        var firstCoord = (reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        var nextZoneNum = reader.ReadInt32();
        var xyz = new (float X, float Y, float Z)[SlotCount];
        for (var i = 0; i < SlotCount; i++)
            xyz[i] = (reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        var nextZone = reader.ReadInt32Array(SlotCount);

        var startCoordNum = reader.ReadInt32();
        var startCoord = new (float X, float Y, float Z)[SlotCount];
        for (var i = 0; i < SlotCount; i++)
            startCoord[i] = (reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        var startCoordZone = reader.ReadInt32Array(SlotCount);

        return new ZoneMoveDataRecord(zoneNumber, firstCoord, nextZoneNum, xyz, nextZone, startCoordNum, startCoord,
            startCoordZone);
    }
}
