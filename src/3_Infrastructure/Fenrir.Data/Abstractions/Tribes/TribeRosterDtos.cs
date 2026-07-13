using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Tribes;

[GenerateDto]
public sealed partial record TribeRosterCharacterDto(
    byte TribeId,
    short Level1,
    short Level2,
    short RebirthCount);
