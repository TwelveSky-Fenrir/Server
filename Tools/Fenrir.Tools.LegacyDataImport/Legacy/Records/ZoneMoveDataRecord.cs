namespace Fenrir.Tools.LegacyDataImport.Legacy.Records;

/// <summary>
///     Faithful in-memory shape of one legacy <c>ZONEMOVEDATA</c> record (Header/Protocol/STRUCT.h:1369-1378).
///     The file (<c>003.BIN</c>) holds exactly <c>MAX_ZONE_NUMBER_NUM</c> (350) of these back-to-back with no
///     header and no explicit zone-number field, so <see cref="ZoneNumber" /> is assigned by the reader from the
///     record's 0-based array index + 1 (record[zoneNumber-1] == this zone's data).
/// </summary>
/// <param name="ZoneNumber">1-based zone number, derived from this record's position in the file.</param>
/// <param name="FirstCoord">
///     Default spawn point for this zone; mirrors the legacy <c>mFirstCoord[3]</c> as an (X, Y, Z)
///     tuple rather than <c>float[3]</c> for readability.
/// </param>
/// <param name="NextZoneNum">
///     Count of live outbound portal entries in <see cref="Xyz" />/<see cref="NextZone" /> (the rest
///     of each 100-slot array is unused).
/// </param>
/// <param name="Xyz">
///     Outbound portal trigger coordinate, one per exit, for i in 0..<see cref="NextZoneNum" />-1; mirrors
///     <c>mXYZ[100][3]</c>.
/// </param>
/// <param name="NextZone">Destination zone number for the matching <see cref="Xyz" /> entry.</param>
/// <param name="StartCoordNum">
///     Count of live inbound-landing entries in <see cref="StartCoord" />/
///     <see cref="StartCoordZone" />.
/// </param>
/// <param name="StartCoord">
///     Inbound landing coordinate, keyed by the zone arrived FROM, for i in 0..
///     <see cref="StartCoordNum" />-1; mirrors <c>mStartCoord[100][3]</c>.
/// </param>
/// <param name="StartCoordZone">
///     The zone number this <see cref="StartCoord" /> entry's landing point applies to when
///     arriving from it.
/// </param>
internal sealed record ZoneMoveDataRecord(
    int ZoneNumber,
    (float X, float Y, float Z) FirstCoord,
    int NextZoneNum,
    (float X, float Y, float Z)[] Xyz,
    int[] NextZone,
    int StartCoordNum,
    (float X, float Y, float Z)[] StartCoord,
    int[] StartCoordZone);
