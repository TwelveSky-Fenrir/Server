using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Readers;

/// <summary>
///     Parses <c>005_00005.IMG</c> into <see cref="NpcRecord" /> instances (Header/Protocol/STRUCT.h:213-234,
///     <c>NPC_INFO</c>, 11736 bytes/record). No known runtime per-load patches are applied to NPC data after
///     unpacking -- unlike items, there is no equivalent of <c>MyShm::Load_Item</c>'s post-load fixups for NPCs,
///     so <see cref="ReadAll" /> simply delegates to <see cref="ReadAllRaw" />.
/// </summary>
internal static class NpcReader
{
    private const string FileName = "005_00005.IMG";
    private const int XorKey = 0x86A;
    private const int RecordArrayOffset = 36;
    private const int RecordCount = 500;
    private const int RecordSize = 11736;

    /// <summary>Raw parse, exactly as bytes on disk -- no runtime patches applied (there are none known for NPCs).</summary>
    public static IReadOnlyList<NpcRecord> ReadAllRaw(string dataDirectory)
    {
        var recordBytes = ImgUnpacker.UnpackRecordArray(
            Path.Combine(dataDirectory, FileName), XorKey, RecordArrayOffset, RecordCount, RecordSize);

        var npcs = new List<NpcRecord>(RecordCount);
        for (var i = 0; i < RecordCount; i++)
            npcs.Add(ReadOne(recordBytes.AsSpan(i * RecordSize, RecordSize)));

        return npcs;
    }

    /// <summary>No known runtime patches apply to NPC data, so this is identical to <see cref="ReadAllRaw" />.</summary>
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

        // nSpeech[MAX_NPC_SPEECH_NUM1=5][MAX_NPC_SPEECH_NUM2=5][MAX_NPC_SPEECH_LENGTH=51] -- read as 25 separate
        // fixed strings in declaration order; Speech[outer][inner] mirrors the C array indexing exactly.
        var speech = new string[5][];
        for (var outer = 0; outer < 5; outer++)
        {
            var inner = new string[5];
            for (var j = 0; j < 5; j++) inner[j] = reader.ReadFixedString(51);
            speech[outer] = inner;
        }

        reader.Skip(1); // compiler padding before nTribe (offset 1311 -> 1312)

        var tribe = reader.ReadInt32();
        var type = reader.ReadInt32();
        var dataSortNumber2D = reader.ReadInt32();
        var dataSortNumber3D = reader.ReadInt32();
        var size = reader.ReadInt32Array(3);
        var menu = reader.ReadInt32Array(100);

        // nShopInfo[MAX_NPC_SHOP_PAGE_NUM=3][MAX_NPC_SHOP_SLOT_NUM=28] -- ShopInfo[page][slot].
        var shopInfo = new int[3][];
        for (var page = 0; page < 3; page++) shopInfo[page] = reader.ReadInt32Array(28);

        // nSkillInfo1[3][8].
        var skillInfo1 = new int[3][];
        for (var i = 0; i < 3; i++) skillInfo1[i] = reader.ReadInt32Array(8);

        // nSkillInfo2[3][3][3][8] = 216 ints, flattened; [a,b,c,d] -> a*72 + b*24 + c*8 + d.
        var skillInfo2 = reader.ReadInt32Array(216);

        // nGambleCostInfo[145][15] -- GambleCostInfo[row][col].
        var gambleCostInfo = new int[145][];
        for (var row = 0; row < 145; row++) gambleCostInfo[row] = reader.ReadInt32Array(15);

        return new NpcRecord(
            index, name, speechNum, speech, tribe, type, dataSortNumber2D, dataSortNumber3D, size, menu,
            shopInfo, skillInfo1, skillInfo2, gambleCostInfo);
    }
}
