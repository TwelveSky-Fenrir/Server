using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Security;

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
