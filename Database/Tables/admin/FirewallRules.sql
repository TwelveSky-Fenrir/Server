-- Legacy `firewall_ip`: `type`'s allow-vs-deny semantics could not be recovered from the legacy dump,
-- so RuleType is stored as an unconfirmed passthrough TINYINT. Its legitimate value range (0-5) IS
-- recovered though -- FIREWALL_TYPE in Server/ts25firewall/firewall.h:20-27 (TCP_ALLOW/TCP_BLOCK/ANY_ALLOW/
-- ANY_BLOCK/TCP_ALLOW_CF/TCP_ALLOW_IPRANGE) -- hence the CHECK below.
CREATE TABLE admin.FirewallRules
(
    FirewallRuleId INT IDENTITY (1,1) NOT NULL,
    IpAddress      VARCHAR(45)        NOT NULL,
    RuleType       TINYINT            NOT NULL,
    CONSTRAINT PK_FirewallRules PRIMARY KEY CLUSTERED (FirewallRuleId),
    CONSTRAINT UQ_FirewallRules_IpAddress UNIQUE (IpAddress),
    CONSTRAINT CK_FirewallRules_RuleType CHECK (RuleType BETWEEN 0 AND 5)
);
