using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

[GenerateDto]
public sealed partial record ZoneRowDto(
    short ZoneNumber,
    float DefaultSpawnX,
    float DefaultSpawnY,
    float DefaultSpawnZ);

[GenerateDto]
public sealed partial record ZonePortalRowDto(
    short ZoneNumber,
    short SlotIndex,
    float TriggerX,
    float TriggerY,
    float TriggerZ,
    short? TargetZoneNumber);

[GenerateDto]
public sealed partial record ZoneSpawnPointRowDto(
    short ZoneNumber,
    short SlotIndex,
    short? FromZoneNumber,
    float PosX,
    float PosY,
    float PosZ);

[GenerateDto]
public sealed partial record ZoneNpcSpawnRowDto(
    short ZoneNumber,
    short SlotIndex,
    int? NpcId,
    float PosX,
    float PosY,
    float PosZ,
    float Angle);

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
