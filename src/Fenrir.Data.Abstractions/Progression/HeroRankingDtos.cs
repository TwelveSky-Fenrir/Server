using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Progression;

[GenerateDto]
public sealed partial record HeroRankingRowDto(
    int CharacterId,
    string CharacterName,
    byte? TribeId,
    int Points,
    short? Level,
    bool? RewardClaimed,
    string? Description,
    DateTime RecordedAtUtc);

[GenerateDto]
public sealed partial record HeroRankingPointsDto(int Points);
