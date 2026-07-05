using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Security;

// admin.usp_BlockedIp_Exists; IsBlockedAsync only checks row count, full shape kept for future GM tooling.
[GenerateDto]
public sealed partial record BlockedIpRowDto(
    int BlockedIpId,
    DateTime CreatedAtUtc);
