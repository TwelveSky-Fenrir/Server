namespace Fenrir.Tools.DbMigrator.Legacy.Records;

internal sealed record ZoneMoveDataRecord(
    int ZoneNumber,
    (float X, float Y, float Z) FirstCoord,
    int NextZoneNum,
    (float X, float Y, float Z)[] Xyz,
    int[] NextZone,
    int StartCoordNum,
    (float X, float Y, float Z)[] StartCoord,
    int[] StartCoordZone);
