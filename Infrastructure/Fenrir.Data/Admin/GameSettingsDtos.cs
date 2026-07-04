using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Admin;

/// <summary>The singleton admin.usp_GameSettings_Get row -- ordinal contract: ProxyShopDurationDays.</summary>
[GenerateDto]
public sealed partial record GameSettingsDto(byte ProxyShopDurationDays);
