using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Progression;

/// <summary>game.usp_TowerState_GetAll, ordered by TowerIndex.</summary>
[GenerateDto]
public sealed partial record TowerStateRowDto(
    byte TowerIndex,
    byte Level,
    byte TowerType,
    byte? ControllingTribeId,
    DateTime? CapturedAtUtc);
