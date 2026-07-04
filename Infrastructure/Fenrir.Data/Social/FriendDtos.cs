using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Social;

/// <summary>One occupied friend slot (game.usp_CharacterFriend_GetByCharacter) -- ordinal contract: Slot, FriendCharacterId, FriendName.</summary>
[GenerateDto]
public sealed partial record CharacterFriendDto(
    byte Slot,
    int FriendCharacterId,
    string FriendName);
