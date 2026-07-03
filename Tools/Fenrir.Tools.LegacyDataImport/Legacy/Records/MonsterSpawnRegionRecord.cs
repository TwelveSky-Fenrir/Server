namespace Fenrir.Tools.LegacyDataImport.Legacy.Records;

/// <summary>
///     Faithful in-memory shape of one row of a legacy <c>WORLD_REGION_INFO</c> record
///     (Header/Protocol/STRUCT.h:1327-1335), as read from a <c>*.WREGION.csv</c> file under
///     <c>DATA/SUMMON/</c> -- the pipe-delimited text format the legacy server actually loads at runtime
///     (Server/ts25zone/S10_MySummon.cpp); the sibling <c>.WREGION</c> binaries under
///     <c>DATA/SUMMON/WRegion/</c> are dead/unused code in this build and are not read by this project.
/// </summary>
/// <param name="ZoneNumber">The zone number parsed from the "Z0NN" prefix of <see cref="SourceFileName" />.</param>
/// <param name="SourceFileName">
///     The file name (no directory) this row was parsed from -- e.g. <c>"Z040_SUMMONBOSSMONSTER.WREGION.csv"</c> or
///     <c>"Z019_SUMMONMONSTER_3.WREGION.csv"</c>. Deliberately kept as the raw file name rather than parsed into a
///     separate "kind"/"variant" column here: normalizing that is a SQL-schema decision left to a later phase.
/// </param>
/// <param name="Value01">mVALUE01 -- purpose not yet documented upstream.</param>
/// <param name="MonsterIndex">mIndex -- foreign-key-like reference into the Monster dataset.</param>
/// <param name="Value03">mVALUE03 -- purpose not yet documented upstream.</param>
/// <param name="Number">mNumber -- number of monsters to summon at this region.</param>
/// <param name="Location">
///     mLocation[3] -- spawn location, in <c>[x, y, z]</c> order (always length 3). Kept as an array, mirroring
///     the array-typed fields already established in <c>ItemRecord</c>, rather than three separate X/Y/Z members.
/// </param>
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
