using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Admin;

// admin.usp_Mute_GetActiveForCharacter; IsActiveForCharacterAsync only checks row count (client sees a bool), full shape kept for future GM tooling.
[GenerateDto]
public sealed partial record MuteRowDto(
    int MuteId,
    int? AccountId,
    int? CharacterId,
    byte Reason,
    DateTime? ExpiresAtUtc,
    DateTime CreatedAtUtc);

// admin.usp_Mute_GetActiveForCharacters (batched); GetActiveCharacterIdsAsync only needs the id back, one
// row per currently-muted character in the supplied set.
[GenerateDto]
public sealed partial record MutedCharacterIdDto(int CharacterId);
