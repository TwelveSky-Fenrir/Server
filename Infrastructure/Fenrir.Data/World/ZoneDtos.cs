using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.World;

// world.usp_Zone_GetAll; ordinal-mapped, ctor order must match the SELECT.
[GenerateDto]
public sealed partial record ZoneRowDto(
    short ZoneNumber,
    float DefaultSpawnX,
    float DefaultSpawnY,
    float DefaultSpawnZ);

/// <summary>
///     world.usp_ZonePortal_GetAll; TargetZoneNumber null = dead-end trigger or unshipped zone (filtered at
///     cache-build time).
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
///     world.usp_ZoneSpawnPoint_GetAll; ZoneNumber = landing zone, FromZoneNumber = origin zone (null if
///     unrecorded/unshipped).
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
///     world.usp_ZoneNpcSpawn_GetAll; NpcId null when the legacy NPC number resolved to nothing (filtered at
///     cache-build time).
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
///     world.usp_MonsterSpawnRegion_GetAll; ZoneNumber null = unshipped zone, MonsterId null = legacy mIndex 0 (both
///     filtered at cache-build time).
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
