namespace Fenrir.Tools.DbMigrator.Legacy.Records;

internal sealed record ZoneNpcPlacementRecord(
    int ZoneNumber,
    int TotalNpcNum,
    int[] NpcNumber,
    (float X, float Y, float Z)[] NpcCoord,
    float[] NpcAngle);
