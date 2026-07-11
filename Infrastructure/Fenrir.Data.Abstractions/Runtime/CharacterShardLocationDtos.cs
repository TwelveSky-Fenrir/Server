using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

[GenerateDto]
public sealed partial record CharacterShardLocationDto(
    int CharacterId,
    byte ShardId,
    short MapId,
    string AvatarName,
    byte Tribe,
    DateTime LastSeenUtc);
