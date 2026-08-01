using Fenrir.Tools.DbMigrator.Legacy.Records;

namespace Fenrir.Tools.DbMigrator.Legacy.Readers;

internal static class SocketReader
{
    private const string FileName = "005_00010.IMG";
    private const int XorKey = 0x24A6;
    private const int RecordArrayOffset = 26;
    private const int RecordCount = 2891;
    private const int RecordSize = 20;

    public static IReadOnlyList<SocketRecord> ReadAllRaw(string dataDirectory)
    {
        var recordBytes = ImgUnpacker.UnpackRecordArray(
            Path.Combine(dataDirectory, FileName), XorKey, RecordArrayOffset, RecordCount, RecordSize);

        var sockets = new List<SocketRecord>(RecordCount);
        for (var i = 0; i < RecordCount; i++)
            sockets.Add(ReadOne(recordBytes.AsSpan(i * RecordSize, RecordSize)));

        return sockets;
    }

    public static IReadOnlyList<SocketRecord> ReadAll(string dataDirectory)
    {
        return ReadAllRaw(dataDirectory);
    }

    private static SocketRecord ReadOne(ReadOnlySpan<byte> record)
    {
        var reader = new LegacySpanReader(record);

        var type = reader.ReadInt32();
        var value01 = reader.ReadInt32();
        var value02 = reader.ReadInt32();
        var value03 = reader.ReadInt32();
        var value04 = reader.ReadInt32();

        return new SocketRecord(type, value01, value02, value03, value04);
    }
}
