using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Admin;

[GenerateDto]
public sealed partial record GameSettingsDto(byte ProxyShopDurationDays);
