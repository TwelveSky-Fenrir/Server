using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Tribes;

/// <summary>
///     One max-level character's tribe/level/rebirth fields for the level-based tribe-point recompute
///     (game.usp_TribeRoster_GetForTribePoint) -- ordinal contract. Maps 1:1 onto the domain's
///     TribeRosterCharacterSnapshot: TribeId (aTribe), Level1 (aLevel1), Level2 (aLevel2), RebirthCount
///     (aRebirthNum). RebirthCount is projected as SMALLINT by the procedure (source column is INT, capped
///     0-12) so it binds to <see cref="short" /> here.
/// </summary>
[GenerateDto]
public sealed partial record TribeRosterCharacterDto(
    byte TribeId,
    short Level1,
    short Level2,
    short RebirthCount);
