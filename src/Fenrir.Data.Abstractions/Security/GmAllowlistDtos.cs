using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Security;

[GenerateDto]
public sealed partial record GmAllowlistRowDto(
    int GmAllowlistId,
    string IpAddress,
    DateTime CreatedAtUtc);
