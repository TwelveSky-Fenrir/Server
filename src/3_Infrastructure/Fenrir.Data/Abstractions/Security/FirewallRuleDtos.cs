using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Security;

[GenerateDto]
public sealed partial record FirewallRuleRowDto(
    int FirewallRuleId,
    string IpAddress,
    byte RuleType);
