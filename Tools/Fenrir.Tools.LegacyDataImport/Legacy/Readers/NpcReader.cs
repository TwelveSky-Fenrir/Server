using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Readers;

internal static class NpcReader
{
    private const string FileName = "005_00005.IMG";
    private const int XorKey = 0x86A;
    private const int RecordArrayOffset = 36;
    private const int RecordCount = 500;
    private const int RecordSize = 11736;

    public static IReadOnlyList<NpcRecord> ReadAllRaw(string dataDirectory)
    {
        var recordBytes = ImgUnpacker.UnpackRecordArray(
            Path.Combine(dataDirectory, FileName), XorKey, RecordArrayOffset, RecordCount, RecordSize);

        var npcs = new List<NpcRecord>(RecordCount);
        for (var i = 0; i < RecordCount; i++)
            npcs.Add(ReadOne(recordBytes.AsSpan(i * RecordSize, RecordSize)));

        return npcs;
    }

    public static IReadOnlyList<NpcRecord> ReadAll(string dataDirectory)
    {
        return ReadAllRaw(dataDirectory);
    }

    private static NpcRecord ReadOne(ReadOnlySpan<byte> record)
    {
        var reader = new LegacySpanReader(record);

        var index = reader.ReadInt32();
        var name = reader.ReadFixedString(28);
        var speechNum = reader.ReadInt32();

        var speech = new string[5][];
        for (var outer = 0; outer < 5; outer++)
        {
            var inner = new string[5];
            for (var j = 0; j < 5; j++) inner[j] = reader.ReadFixedString(51);
            speech[outer] = inner;
        }

        reader.Skip(1);

        var tribe = reader.ReadInt32();
        var type = reader.ReadInt32();
        var dataSortNumber2D = reader.ReadInt32();
        var dataSortNumber3D = reader.ReadInt32();
        var size = reader.ReadInt32Array(3);
        var menu = reader.ReadInt32Array(100);

        var shopInfo = new int[3][];
        for (var page = 0; page < 3; page++) shopInfo[page] = reader.ReadInt32Array(28);

        var skillInfo1 = new int[3][];
        for (var i = 0; i < 3; i++) skillInfo1[i] = reader.ReadInt32Array(8);

        var skillInfo2 = reader.ReadInt32Array(216);

        var gambleCostInfo = new int[145][];
        for (var row = 0; row < 145; row++) gambleCostInfo[row] = reader.ReadInt32Array(15);

        return new NpcRecord(
            index, name, speechNum, speech, tribe, type, dataSortNumber2D, dataSortNumber3D, size, menu,
            shopInfo, skillInfo1, skillInfo2, gambleCostInfo);
    }
}
