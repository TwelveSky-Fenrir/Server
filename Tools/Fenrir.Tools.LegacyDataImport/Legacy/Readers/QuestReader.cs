using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Readers;

/// <summary>
///     Parses <c>005_00006.IMG</c> (legacy <c>QUEST_INFO</c> table). No known runtime per-load patches --
///     <see cref="ReadAll" /> simply delegates to <see cref="ReadAllRaw" />.
/// </summary>
internal static class QuestReader
{
    private const string FileName = "005_00006.IMG";
    private const int XorKey = 0; // no-op XOR for this dataset -- still validated against the expected count
    private const int RecordArrayOffset = 4;
    private const int RecordCount = 1000;
    private const int RecordSize = 8444;

    /// <summary>Raw parse, exactly as bytes on disk -- no runtime patches applied.</summary>
    public static IReadOnlyList<QuestRecord> ReadAllRaw(string dataDirectory)
    {
        var recordBytes = ImgUnpacker.UnpackRecordArray(
            Path.Combine(dataDirectory, FileName), XorKey, RecordArrayOffset, RecordCount, RecordSize);

        var quests = new List<QuestRecord>(RecordCount);
        for (var i = 0; i < RecordCount; i++)
            quests.Add(ReadOne(recordBytes.AsSpan(i * RecordSize, RecordSize)));

        return quests;
    }

    /// <summary>No known runtime patches for this dataset -- identical to <see cref="ReadAllRaw" />.</summary>
    public static IReadOnlyList<QuestRecord> ReadAll(string dataDirectory)
    {
        return ReadAllRaw(dataDirectory);
    }

    private static QuestRecord ReadOne(ReadOnlySpan<byte> record)
    {
        var reader = new LegacySpanReader(record);

        var index = reader.ReadInt32();
        var subject = reader.ReadFixedString(51);
        reader.Skip(1); // compiler padding before qCategory (offset 55 -> 56)

        var category = reader.ReadInt32();
        var step = reader.ReadInt32();
        var level = reader.ReadInt32();
        var type = reader.ReadInt32();
        var sort = reader.ReadInt32();
        var summonInfo = reader.ReadInt32Array(4);
        var startNpcNumber = reader.ReadInt32();
        var keyNpcNumber = reader.ReadInt32Array(5);
        var endNpcNumber = reader.ReadInt32();
        var solution = reader.ReadInt32Array(4);
        var reward = new int[3][];
        for (var i = 0; i < 3; i++) reward[i] = reader.ReadInt32Array(2);
        var nextIndex = reader.ReadInt32();

        var (startSpeech, startSpeechColor) = ReadSpeechBlock(ref reader);
        var (hurrySpeech, hurrySpeechColor) = ReadSpeechBlock(ref reader);
        var (processSpeech1, processSpeech1Color) = ReadSpeechBlock(ref reader);
        var (processSpeech2, processSpeech2Color) = ReadSpeechBlock(ref reader);
        var (processSpeech3, processSpeech3Color) = ReadSpeechBlock(ref reader);
        var (processSpeech4, processSpeech4Color) = ReadSpeechBlock(ref reader);
        var (processSpeech5, processSpeech5Color) = ReadSpeechBlock(ref reader);
        var (successSpeech, successSpeechColor) = ReadSpeechBlock(ref reader);
        var (failureSpeech, failureSpeechColor) = ReadSpeechBlock(ref reader);
        var (callSpeech, callSpeechColor) = ReadSpeechBlock(ref reader);

        return new QuestRecord(
            index, subject, category, step, level, type, sort, summonInfo, startNpcNumber, keyNpcNumber,
            endNpcNumber, solution, reward, nextIndex,
            startSpeech, startSpeechColor,
            hurrySpeech, hurrySpeechColor,
            processSpeech1, processSpeech1Color,
            processSpeech2, processSpeech2Color,
            processSpeech3, processSpeech3Color,
            processSpeech4, processSpeech4Color,
            processSpeech5, processSpeech5Color,
            successSpeech, successSpeechColor,
            failureSpeech, failureSpeechColor,
            callSpeech, callSpeechColor);
    }

    /// <summary>
    ///     Reads one <c>char[15][51]</c> dialogue-line block followed by 3 bytes of compiler padding and its
    ///     matching <c>int[15]</c> per-line color array.
    /// </summary>
    private static (string[] Lines, int[] Colors) ReadSpeechBlock(ref LegacySpanReader reader)
    {
        var lines = new string[15];
        for (var i = 0; i < 15; i++) lines[i] = reader.ReadFixedString(51);
        reader.Skip(3); // compiler padding before the color array
        var colors = reader.ReadInt32Array(15);
        return (lines, colors);
    }
}
