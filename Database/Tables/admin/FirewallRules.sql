CREATE TABLE admin.FirewallRules
(
    FirewallRuleId INT IDENTITY (1,1) NOT NULL,
    IpAddress      VARCHAR(45)        NOT NULL,
    RuleType       TINYINT            NOT NULL,
    CreatedAtUtc   DATETIME2(3)       NOT NULL
        CONSTRAINT DF_FirewallRules_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    ExpiresAtUtc   DATETIME2(3)       NULL,
    CONSTRAINT PK_FirewallRules PRIMARY KEY CLUSTERED (FirewallRuleId),
    CONSTRAINT UQ_FirewallRules_IpAddress UNIQUE (IpAddress),
    CONSTRAINT CK_FirewallRules_RuleType CHECK (RuleType BETWEEN 0 AND 5)
);
