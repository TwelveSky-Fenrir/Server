using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Security;

// admin.usp_MacRestriction_GetAll; ordinal-mapped.
[GenerateDto]
public sealed partial record MacRestrictionRowDto(
    int MacRestrictionId,
    string MacAddress,
    string? MachineGuid,
    int AccountLimit);
