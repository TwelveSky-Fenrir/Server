using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Admin;

/// <summary>One row of admin.usp_ShardMapAssignment_GetForShard -- ordinal contract: MapId.</summary>
[GenerateDto]
public sealed partial record ShardMapAssignmentRowDto(short MapId);
