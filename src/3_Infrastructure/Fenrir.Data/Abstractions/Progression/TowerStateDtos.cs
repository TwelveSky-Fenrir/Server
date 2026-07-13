using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Progression;

[GenerateDto]
public sealed partial record TowerStateRowDto(
    byte TowerIndex,
    byte Level,
    byte TowerType,
    byte? ControllingTribeId,
    DateTime? CapturedAtUtc);
