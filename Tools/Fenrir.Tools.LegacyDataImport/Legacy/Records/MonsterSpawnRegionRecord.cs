namespace Fenrir.Tools.LegacyDataImport.Legacy.Records;

/// <summary>
///     Legacy <c>WORLD_REGION_INFO</c> row (STRUCT.h:1327-1335) parsed from <c>*.WREGION.csv</c> under DATA/SUMMON --
///     the live format (S10_MySummon.cpp); sibling <c>.WREGION</c> binaries are dead code, not read here.
/// </summary>
/// <param name="ZoneNumber">Parsed from the "Z0NN" prefix of <see cref="SourceFileName" />.</param>
/// <param name="SourceFileName">Raw source file name, e.g. <c>"Z040_SUMMONBOSSMONSTER.WREGION.csv"</c>.</param>
/// <param name="Value01">mVALUE01 -- purpose not documented upstream.</param>
/// <param name="MonsterIndex">mIndex -- foreign-key-like reference into the Monster dataset.</param>
/// <param name="Value03">mVALUE03 -- purpose not documented upstream.</param>
/// <param name="Number">mNumber -- number of monsters to summon at this region.</param>
/// <param name="Location">mLocation[3] -- spawn location, in <c>[x, y, z]</c> order.</param>
/// <param name="Radius">mRADIUS -- spawn radius around <see cref="Location" />.</param>
internal sealed record MonsterSpawnRegionRecord(
    int ZoneNumber,
    string SourceFileName,
    int Value01,
    int MonsterIndex,
    int Value03,
    int Number,
    int[] Location,
    int Radius);
