using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Admin;

// admin.usp_Mute_GetActiveForCharacter; IsActiveForCharacterAsync only checks row count (client sees a bool), full shape kept for future GM tooling.
[GenerateDto]
public sealed partial record MuteRowDto(
    int MuteId,
    int? AccountId,
    int? CharacterId,
    byte Reason,
    DateTime? ExpiresAtUtc,
    DateTime CreatedAtUtc);
