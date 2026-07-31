namespace Fenrir.Tools.DbMigrator.Legacy.Records;

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
