using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Admin;

[GenerateDto]
public sealed partial record MuteRowDto(
    int MuteId,
    int? AccountId,
    int? CharacterId,
    byte Reason,
    DateTime? ExpiresAtUtc,
    DateTime CreatedAtUtc);

[GenerateDto]
public sealed partial record MutedCharacterIdDto(int CharacterId);
