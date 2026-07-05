using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Security;

// admin.usp_GmAllowlist_GetAll; ordinal-mapped.
[GenerateDto]
public sealed partial record GmAllowlistRowDto(
    int GmAllowlistId,
    string IpAddress,
    DateTime CreatedAtUtc);
