using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Legacy.Readers;

internal static class QuestReader
{
    private const string FileName = "005_00006.IMG";
    private const int XorKey = 0;
    private const int RecordArrayOffset = 4;
    private const int RecordCount = 1000;
    private const int RecordSize = 8444;

    public static IReadOnlyList<QuestRecord> ReadAllRaw(string dataDirectory)
    {
        var recordBytes = ImgUnpacker.UnpackRecordArray(
            Path.Combine(dataDirectory, FileName), XorKey, RecordArrayOffset, RecordCount, RecordSize);

        var quests = new List<QuestRecord>(RecordCount);
        for (var i = 0; i < RecordCount; i++)
            quests.Add(ReadOne(recordBytes.AsSpan(i * RecordSize, RecordSize)));

        return quests;
    }

    public static IReadOnlyList<QuestRecord> ReadAll(string dataDirectory)
    {
        return ReadAllRaw(dataDirectory);
    }

    private static QuestRecord ReadOne(ReadOnlySpan<byte> record)
    {
        var reader = new LegacySpanReader(record);

        var index = reader.ReadInt32();
        var subject = reader.ReadFixedString(51);
        reader.Skip(1);

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

        private static (string[] Lines, int[] Colors) ReadSpeechBlock(ref LegacySpanReader reader)
    {
        var lines = new string[15];
        for (var i = 0; i < 15; i++) lines[i] = reader.ReadFixedString(51);
        reader.Skip(3);
        var colors = reader.ReadInt32Array(15);
        return (lines, colors);
    }
}
