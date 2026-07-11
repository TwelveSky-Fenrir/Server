using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Security;

[GenerateDto]
public sealed partial record MacRestrictionRowDto(
    int MacRestrictionId,
    string MacAddress,
    string? MachineGuid,
    int AccountLimit);
