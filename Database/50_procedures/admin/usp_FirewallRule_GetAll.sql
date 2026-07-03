-- Contract: no parameters -> RS0, every row. Called once at LoginServer/GameServer boot to populate an
-- in-memory rule cache (see admin.FirewallRules's table comment -- a small, rarely-changing operational
-- list, treated like world.* reference data rather than a per-connection query).
CREATE PROCEDURE admin.usp_FirewallRule_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT FirewallRuleId, IpAddress, RuleType
FROM admin.FirewallRules;
END;
