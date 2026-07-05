using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Social;

/// <summary>game.usp_CharacterFriend_GetByCharacter; one occupied friend slot.</summary>
[GenerateDto]
public sealed partial record CharacterFriendDto(
    byte Slot,
    int FriendCharacterId,
    string FriendName);
