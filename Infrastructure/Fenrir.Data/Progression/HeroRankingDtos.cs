using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Progression;

/// <summary>
///     One game.HeroRankings row, joined to the character's display name -- ordinal contract of
///     game.usp_HeroRanking_GetByPeriod's single result set (report 12/contracts 07: HERO_RANK, CZ 118 ->
///     ZC 148/150). Ordered Points DESC by the proc itself (leaderboard order).
/// </summary>
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
