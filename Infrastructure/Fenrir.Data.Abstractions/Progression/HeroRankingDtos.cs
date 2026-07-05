using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Progression;

// game.usp_HeroRanking_GetByPeriod; already ordered Points DESC by the proc (leaderboard order).
[GenerateDto]
public sealed partial record HeroRankingRowDto(
    int CharacterId,
    string CharacterName,
    byte? TribeId,
    int Points,
    int? Level,
    bool? RewardClaimed,
    string? Description,
    DateTime RecordedAtUtc);
