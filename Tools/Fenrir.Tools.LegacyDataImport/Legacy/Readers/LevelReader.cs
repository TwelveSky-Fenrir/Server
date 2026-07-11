using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Readers;

internal static class LevelReader
{
    private const string FileName = "005_00001.IMG";
    private const int XorKey = 0x217B;
    private const int RecordArrayOffset = 14;
    private const int RecordCount = 145;
    private const int RecordSize = 44;

    public static IReadOnlyList<LevelRecord> ReadAllRaw(string dataDirectory)
    {
        var recordBytes = ImgUnpacker.UnpackRecordArray(
            Path.Combine(dataDirectory, FileName), XorKey, RecordArrayOffset, RecordCount, RecordSize);

        var levels = new List<LevelRecord>(RecordCount);
        for (var i = 0; i < RecordCount; i++)
            levels.Add(ReadOne(recordBytes.AsSpan(i * RecordSize, RecordSize)));

        return levels;
    }

    public static IReadOnlyList<LevelRecord> ReadAll(string dataDirectory)
    {
        return ReadAllRaw(dataDirectory);
    }

    private static LevelRecord ReadOne(ReadOnlySpan<byte> record)
    {
        var reader = new LegacySpanReader(record);

        var index = reader.ReadInt32();
        var rangeInfo = reader.ReadInt32Array(3);
        var attackPower = reader.ReadInt32();
        var defensePower = reader.ReadInt32();
        var attackSuccess = reader.ReadInt32();
        var attackBlock = reader.ReadInt32();
        var elementAttack = reader.ReadInt32();
        var life = reader.ReadInt32();
        var mana = reader.ReadInt32();

        return new LevelRecord(
            index, rangeInfo, attackPower, defensePower, attackSuccess, attackBlock, elementAttack, life, mana);
    }
}
