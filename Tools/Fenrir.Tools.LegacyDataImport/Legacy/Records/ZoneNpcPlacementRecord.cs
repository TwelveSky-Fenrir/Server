namespace Fenrir.Tools.LegacyDataImport.Legacy.Records;

/// <summary>
///     Faithful in-memory shape of one legacy <c>ZONENPCINFODATA</c> record (Header/Protocol/STRUCT.h:1380-1386).
///     The file (<c>002.BIN</c>) holds exactly <c>MAX_ZONE_NUMBER_NUM</c> (350) of these back-to-back with no
///     header and no explicit zone-number field, so <see cref="ZoneNumber" /> is assigned by the reader from the
///     record's 0-based array index + 1 (record[zoneNumber-1] == this zone's data).
/// </summary>
/// <param name="ZoneNumber">1-based zone number, derived from this record's position in the file.</param>
/// <param name="TotalNpcNum">
///     Count of live entries in <see cref="NpcNumber" />/<see cref="NpcCoord" />/
///     <see cref="NpcAngle" /> (the rest of each 100-slot array is unused padding for this zone).
/// </param>
/// <param name="NpcNumber">
///     Foreign-key-like references into the Npc dataset, one per placed NPC (only the first
///     <see cref="TotalNpcNum" /> entries are meaningful).
/// </param>
/// <param name="NpcCoord">
///     World-space (X, Y, Z) spawn coordinate per placed NPC; mirrors the legacy
///     <c>mNPCCoord[100][3]</c> as a tuple array rather than a jagged <c>float[100][3]</c> for readability.
/// </param>
/// <param name="NpcAngle">Facing angle per placed NPC.</param>
internal sealed record ZoneNpcPlacementRecord(
    int ZoneNumber,
    int TotalNpcNum,
    int[] NpcNumber,
    (float X, float Y, float Z)[] NpcCoord,
    float[] NpcAngle);
