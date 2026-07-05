namespace Fenrir.Tools.LegacyDataImport.Legacy.Records;

/// <summary>
///     Legacy <c>NPC_INFO</c> (STRUCT.h:213-234, non-GXCW_SV branch -- never defined in this build).
///     <see cref="SkillInfo2" /> flattens <c>nSkillInfo2[3][3][3][8]</c>: <c>[a,b,c,d] -> a*72 + b*24 + c*8 + d</c>.
///     Other jagged arrays (Speech[5][5], ShopInfo[3][28], SkillInfo1[3][8], GambleCostInfo[145][15]) mirror
///     their legacy dimensions directly.
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
