using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

// Ordinal-mapped: ctor order must match usp_CharacterShardLocation_FindByName/FindByCharacterId's SELECT order.
[GenerateDto]
public sealed partial record CharacterShardLocationDto(
    int CharacterId,
    byte ShardId,
    short MapId,
    string AvatarName,
    byte Tribe,
    DateTime LastSeenUtc);
