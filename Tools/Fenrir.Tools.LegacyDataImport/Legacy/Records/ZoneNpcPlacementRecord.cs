namespace Fenrir.Tools.LegacyDataImport.Legacy.Records;

/// <summary>
///     Legacy <c>ZONENPCINFODATA</c> record (STRUCT.h:1380-1386). <c>002.BIN</c> holds 350 back-to-back with no
///     zone-number field; <see cref="ZoneNumber" /> is the record's 1-based array position.
/// </summary>
/// <param name="ZoneNumber">1-based, derived from array position.</param>
/// <param name="TotalNpcNum">
///     Count of live entries in <see cref="NpcNumber" />/<see cref="NpcCoord" />/
///     <see cref="NpcAngle" />.
/// </param>
/// <param name="NpcNumber">Foreign-key-like references into the Npc dataset, one per placed NPC.</param>
/// <param name="NpcCoord">World-space (X, Y, Z) spawn coordinate per placed NPC.</param>
/// <param name="NpcAngle">Facing angle per placed NPC.</param>
internal sealed record ZoneNpcPlacementRecord(
    int ZoneNumber,
    int TotalNpcNum,
    int[] NpcNumber,
    (float X, float Y, float Z)[] NpcCoord,
    float[] NpcAngle);
