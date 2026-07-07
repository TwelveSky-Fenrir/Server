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

/// <summary>
///     One-row result of game.usp_HeroRanking_GetPoints -- the character's stored Points for one
///     (CharacterId, PeriodKind) pair. Row-absent (mapped to a null return by
///     <see cref="IHeroRankingRepository.GetPointsAsync" />) means the character has no row yet for that
///     period (never earned a hero-rank point during it), which is a legitimate zero, not an error.
/// </summary>
[GenerateDto]
public sealed partial record HeroRankingPointsDto(int Points);
