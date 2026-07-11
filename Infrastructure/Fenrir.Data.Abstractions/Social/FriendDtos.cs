using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Social;

[GenerateDto]
public sealed partial record CharacterFriendDto(
    byte Slot,
    int FriendCharacterId,
    string FriendName);
