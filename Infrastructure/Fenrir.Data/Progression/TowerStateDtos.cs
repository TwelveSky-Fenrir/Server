using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Progression;

/// <summary>
///     One game.TowerState row -- ordinal contract of game.usp_TowerState_GetAll's single result set, ordered by
///     TowerIndex.
/// </summary>
[GenerateDto]
public sealed partial record TowerStateRowDto(
    byte TowerIndex,
    byte? ControllingTribeId,
    DateTime? CapturedAtUtc);
