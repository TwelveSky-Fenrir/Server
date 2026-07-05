using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Security;

// admin.usp_FirewallRule_GetAll; ordinal-mapped, RuleType kept as the schema's own "unconfirmed passthrough
// TINYINT" (see FirewallRules.sql) -- FirewallRuleRepository is the only place that assigns it meaning.
[GenerateDto]
public sealed partial record FirewallRuleRowDto(
    int FirewallRuleId,
    string IpAddress,
    byte RuleType);
