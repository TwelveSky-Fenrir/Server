-- Legacy `firewall_ip`: `type`'s allow-vs-deny semantics could not be recovered from the legacy dump,
-- so RuleType is stored as an unconfirmed passthrough TINYINT.
CREATE TABLE admin.FirewallRules
(
    FirewallRuleId INT IDENTITY(1,1) NOT NULL,
    IpAddress      VARCHAR(45) NOT NULL,
    RuleType       TINYINT     NOT NULL,
    CONSTRAINT PK_FirewallRules PRIMARY KEY CLUSTERED (FirewallRuleId),
    CONSTRAINT UQ_FirewallRules_IpAddress UNIQUE (IpAddress)
);
