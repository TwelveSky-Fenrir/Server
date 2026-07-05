using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Security;

// admin.usp_Ban_GetActiveForAccount/usp_Ban_GetActiveForCharacter (both select from admin.vw_ActiveBans);
// IsActiveForAccountAsync/IsActiveForCharacterAsync only check row count, full shape kept for future GM tooling.
[GenerateDto]
public sealed partial record BanRowDto(
    int BanId,
    int? AccountId,
    string? AccountLoginName,
    int? CharacterId,
    string? CharacterName,
    byte Reason,
    DateTime? ExpiresAtUtc,
    DateTime CreatedAtUtc);
