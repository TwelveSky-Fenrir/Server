namespace Fenrir.Tools.LegacyDataImport.Legacy.Records;

/// <summary>
///     Faithful in-memory shape of a legacy <c>QUEST_INFO</c> record (Header/Protocol/STRUCT.h:237-274), 8444
///     bytes total. The struct's tail is ten repetitions of a "speech block" pattern -- a <c>char[15][51]</c>
///     block of dialogue lines immediately followed (after 3 bytes of compiler padding) by a matching
///     <c>int[15]</c> per-line text-color array -- for, in exact declaration order: qStartSpeech, qHurrySpeech,
///     qProcessSpeech1..5, qSuccessSpeech, qFailureSpeech, qCallSpeech.
/// </summary>
internal sealed record QuestRecord(
    int Index,
    string Subject,
    int Category,
    int Step,
    int Level,
    int Type,
    int Sort,
    int[] SummonInfo,
    int StartNPCNumber,
    int[] KeyNPCNumber,
    int EndNPCNumber,
    int[] Solution,
    int[][] Reward,
    int NextIndex,
    string[] StartSpeech,
    int[] StartSpeechColor,
    string[] HurrySpeech,
    int[] HurrySpeechColor,
    string[] ProcessSpeech1,
    int[] ProcessSpeech1Color,
    string[] ProcessSpeech2,
    int[] ProcessSpeech2Color,
    string[] ProcessSpeech3,
    int[] ProcessSpeech3Color,
    string[] ProcessSpeech4,
    int[] ProcessSpeech4Color,
    string[] ProcessSpeech5,
    int[] ProcessSpeech5Color,
    string[] SuccessSpeech,
    int[] SuccessSpeechColor,
    string[] FailureSpeech,
    int[] FailureSpeechColor,
    string[] CallSpeech,
    int[] CallSpeechColor);
