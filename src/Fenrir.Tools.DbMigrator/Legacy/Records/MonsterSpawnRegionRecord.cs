namespace Fenrir.Tools.DbMigrator.Legacy.Records;

internal sealed record MonsterSpawnRegionRecord(
    int ZoneNumber,
    string SourceFileName,
    int Value01,
    int MonsterIndex,
    int Value03,
    int Number,
    int[] Location,
    int Radius);
