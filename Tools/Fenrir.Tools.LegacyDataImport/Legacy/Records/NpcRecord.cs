namespace Fenrir.Tools.LegacyDataImport.Legacy.Records;

/// <summary>
///     Faithful in-memory shape of a legacy <c>NPC_INFO</c> record (Header/Protocol/STRUCT.h:213-234, the
///     non-<c>GXCW_SV</c> branch -- <c>GXCW_SV</c> is never defined in this build).
///     Sub-array indexing conventions (documented here since callers will need them once these are
///     normalized into child tables later):
///     <list type="bullet">
///         <item>
///             <see cref="Speech" /> is jagged <c>string[5][5]</c>: <c>Speech[outer][inner]</c>, mirroring the
///             legacy <c>nSpeech[MAX_NPC_SPEECH_NUM1][MAX_NPC_SPEECH_NUM2][MAX_NPC_SPEECH_LENGTH]</c> declaration
///             order exactly.
///         </item>
///         <item><see cref="ShopInfo" /> is jagged <c>int[3][28]</c>: <c>ShopInfo[page][slot]</c>.</item>
///         <item><see cref="SkillInfo1" /> is jagged <c>int[3][8]</c>.</item>
///         <item>
///             <see cref="SkillInfo2" /> is a flat <c>int[216]</c> standing in for the legacy
///             <c>nSkillInfo2[3][3][3][8]</c>; index <c>[a,b,c,d]</c> maps to
///             <c>a*72 + b*24 + c*8 + d</c> (72 = 3*3*8, 24 = 3*8).
///         </item>
///         <item><see cref="GambleCostInfo" /> is jagged <c>int[145][15]</c>: <c>GambleCostInfo[row][col]</c>.</item>
///     </list>
/// </summary>
internal sealed record NpcRecord(
    int Index,
    string Name,
    int SpeechNum,
    string[][] Speech,
    int Tribe,
    int Type,
    int DataSortNumber2D,
    int DataSortNumber3D,
    int[] Size,
    int[] Menu,
    int[][] ShopInfo,
    int[][] SkillInfo1,
    int[] SkillInfo2,
    int[][] GambleCostInfo);
