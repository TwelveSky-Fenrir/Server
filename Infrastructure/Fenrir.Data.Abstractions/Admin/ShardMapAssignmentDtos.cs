using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Admin;

/// <summary>One row of admin.usp_ShardMapAssignment_GetForShard -- ordinal contract: MapId.</summary>
[GenerateDto]
public sealed partial record ShardMapAssignmentRowDto(short MapId);

/// <summary>One row of admin.usp_ShardMapAssignment_GetAll -- ordinal contract: ShardId, MapId.</summary>
[GenerateDto]
public sealed partial record ShardMapAssignmentDto(byte ShardId, short MapId);
