using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Admin;

/// <summary>
///     One active-or-not mute row (admin.usp_Mute_GetActiveForCharacter) -- ordinal contract: MuteId,
///     AccountId, CharacterId, Reason, ExpiresAtUtc, CreatedAtUtc. Only the ROW COUNT matters to
///     <see cref="MuteRepository.IsActiveForCharacterAsync" /> (report 06 §1.7: a hidden boolean flag,
///     not a structured reason/expiry the client ever sees), but the full row shape is kept for any
///     future GM-tooling caller that needs the detail.
/// </summary>
[GenerateDto]
public sealed partial record MuteRowDto(
    int MuteId,
    int? AccountId,
    int? CharacterId,
    byte Reason,
    DateTime? ExpiresAtUtc,
    DateTime CreatedAtUtc);
