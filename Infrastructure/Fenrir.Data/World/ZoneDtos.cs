using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.World;

/// <summary>
///     One world.Zones row -- ordinal contract of world.usp_Zone_GetAll (117 live zones). Constructor order
///     must track the SELECT column order exactly (invariant I-04); [GenerateDto] maps by position, not by name.
/// </summary>
[GenerateDto]
public sealed partial record ZoneRowDto(
    short ZoneNumber,
    float DefaultSpawnX,
    float DefaultSpawnY,
    float DefaultSpawnZ);

/// <summary>
///     One populated outbound-portal slot -- world.usp_ZonePortal_GetAll (413 rows). TargetZoneNumber is NULL
///     for a trigger volume with no travel destination (~54% of rows) or one naming a zone this build never
///     shipped -- see world.ZonePortals' table header. Those rows are filtered out at cache-build time.
/// </summary>
[GenerateDto]
public sealed partial record ZonePortalRowDto(
    short ZoneNumber,
    short SlotIndex,
    float TriggerX,
    float TriggerY,
    float TriggerZ,
    short? TargetZoneNumber);

/// <summary>
///     One populated inbound-landing slot -- world.usp_ZoneSpawnPoint_GetAll (413 rows). ZoneNumber is the
///     zone you LAND in; FromZoneNumber is which zone you arrived FROM, NULL when the source zone is
///     unrecorded or was never shipped in this build (see world.ZoneSpawnPoints' table header).
/// </summary>
[GenerateDto]
public sealed partial record ZoneSpawnPointRowDto(
    short ZoneNumber,
    short SlotIndex,
    short? FromZoneNumber,
    float PosX,
    float PosY,
    float PosZ);

/// <summary>
///     One populated NPC-placement slot -- world.usp_ZoneNpcSpawn_GetAll (291 rows). NpcId is NULL for a
///     populated slot whose legacy NPC number resolved to no real NPC; those rows are filtered out at
///     cache-build time.
/// </summary>
[GenerateDto]
public sealed partial record ZoneNpcSpawnRowDto(
    short ZoneNumber,
    short SlotIndex,
    int? NpcId,
    float PosX,
    float PosY,
    float PosZ,
    float Angle);

/// <summary>
///     One world.MonsterSpawnRegions row -- world.usp_MonsterSpawnRegion_GetAll (21,960 rows). ZoneNumber is
///     NULL for a region defined against a zone this build never shipped (~49% of rows -- see the table
///     header); MonsterId is NULL for a legacy mIndex of 0. Both cases are filtered out at cache-build time.
/// </summary>
[GenerateDto]
public sealed partial record MonsterSpawnRegionRowDto(
    int MonsterSpawnRegionId,
    short? ZoneNumber,
    string SourceFileName,
    int Value01,
    int? MonsterId,
    int Value03,
    int Number,
    int LocationX,
    int LocationY,
    int LocationZ,
    int Radius);
