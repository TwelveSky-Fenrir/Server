-- Legacy `firewall_ip` (id, ip varchar(128), type smallint(1)) -- 0 rows in the dump, and `type` has no
-- accompanying lookup table or comment anywhere in nxtserver.sql, so its allow-vs-deny (or rule-kind)
-- semantics could not be recovered from the dump alone; stored as a passthrough TINYINT documented here
-- as "legacy passthrough, exact meaning not confirmed" until Fenrir defines its own meaning.
-- Small, rarely-changing operational list -- treated like a world.* reference table (LoginServer/
-- GameServer load the whole thing once at boot via usp_FirewallRule_GetAll), not a per-connection query:
-- no native compilation, this is not the "checked on every single connection" case that
-- admin.BlockedIps/admin.Bans are (this is an internal allow/deny override list, not the primary
-- abuse-blocking path).
CREATE TABLE admin.FirewallRules
(
    FirewallRuleId INT IDENTITY(1,1) NOT NULL,
    IpAddress      VARCHAR(45) NOT NULL,
    RuleType       TINYINT     NOT NULL,
    CONSTRAINT PK_FirewallRules PRIMARY KEY CLUSTERED (FirewallRuleId),
    CONSTRAINT UQ_FirewallRules_IpAddress UNIQUE (IpAddress)
);
