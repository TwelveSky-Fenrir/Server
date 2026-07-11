using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Admin;

[GenerateDto]
public sealed partial record ShardMapAssignmentRowDto(short MapId);

[GenerateDto]
public sealed partial record ShardMapAssignmentDto(byte ShardId, short MapId);
