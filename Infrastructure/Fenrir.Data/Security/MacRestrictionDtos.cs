using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Security;

// admin.usp_MacRestriction_GetAll; ordinal-mapped.
[GenerateDto]
public sealed partial record MacRestrictionRowDto(
    int MacRestrictionId,
    string MacAddress,
    string? MachineGuid,
    int AccountLimit);
