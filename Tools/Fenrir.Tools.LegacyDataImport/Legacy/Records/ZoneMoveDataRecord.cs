namespace Fenrir.Tools.LegacyDataImport.Legacy.Records;

/// <summary>
///     Legacy <c>ZONEMOVEDATA</c> record (STRUCT.h:1369-1378). <c>003.BIN</c> holds 350 back-to-back with no
///     zone-number field; <see cref="ZoneNumber" /> is the record's 1-based array position.
/// </summary>
/// <param name="ZoneNumber">1-based, derived from array position.</param>
/// <param name="FirstCoord">Default spawn point for this zone.</param>
/// <param name="NextZoneNum">Count of live entries in <see cref="Xyz" />/<see cref="NextZone" />.</param>
/// <param name="Xyz">Outbound portal trigger coordinate per exit.</param>
/// <param name="NextZone">Destination zone number for the matching <see cref="Xyz" /> entry.</param>
/// <param name="StartCoordNum">Count of live entries in <see cref="StartCoord" />/<see cref="StartCoordZone" />.</param>
/// <param name="StartCoord">Inbound landing coordinate, keyed by the zone arrived from.</param>
/// <param name="StartCoordZone">Zone number this <see cref="StartCoord" /> entry applies to when arriving from it.</param>
internal sealed record ZoneMoveDataRecord(
    int ZoneNumber,
    (float X, float Y, float Z) FirstCoord,
    int NextZoneNum,
    (float X, float Y, float Z)[] Xyz,
    int[] NextZone,
    int StartCoordNum,
    (float X, float Y, float Z)[] StartCoord,
    int[] StartCoordZone);
