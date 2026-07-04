using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Progression;

/// <summary>game.usp_TowerState_GetAll, ordered by TowerIndex.</summary>
[GenerateDto]
public sealed partial record TowerStateRowDto(
    byte TowerIndex,
    byte? ControllingTribeId,
    DateTime? CapturedAtUtc);
