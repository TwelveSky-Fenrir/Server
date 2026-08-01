using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Security;

[GenerateDto]
public sealed partial record BlockedIpRowDto(
    int BlockedIpId,
    DateTime CreatedAtUtc);
